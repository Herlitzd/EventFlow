﻿// The MIT License (MIT)
//
// Copyright (c) 2020 Rasmus Mikkelsen
// Copyright (c) 2020 eBay Software Foundation
// https://github.com/rasmus/EventFlow
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using Confluent.Kafka;
using EventFlow.Core;
using EventFlow.Logs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlow.Kafka.Integrations
{
    public class KafkaPublisher : IDisposable, IKafkaPublisher
    {
        private readonly ILog _log;
        private readonly IKafkaProducerFactory _producerFactory;
        private readonly ProducerConfig _configuration;
        private readonly ITransientFaultHandler<IKafkaRetryStrategy> _transientFaultHandler;
        private readonly AsyncLock _asyncLock = new AsyncLock();
        private IProducer<string, string> _kafkaProducer;

        public KafkaPublisher(ILog log,
            IKafkaProducerFactory producerFactory,
            ProducerConfig configuration,
            ITransientFaultHandler<IKafkaRetryStrategy> transientFaultHandler)
        {
            _log = log;
            _producerFactory = producerFactory;
            _configuration = configuration;
            _transientFaultHandler = transientFaultHandler;

        }

        public async Task PublishAsync(IReadOnlyCollection<KafkaMessage> kafkaMessages, CancellationToken cancellationToken)
        {
            try
            {
                await _transientFaultHandler.TryAsync(c => (
                    ProduceMessages(kafkaMessages, c)),
                        Label.Named("kafka-publish"),
                        cancellationToken)
                .ConfigureAwait(false);

            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                if (_kafkaProducer != null)
                {
                    using (await _asyncLock.WaitAsync(CancellationToken.None).ConfigureAwait(false))
                    {
                        Dispose();
                    }
                }
                _log.Error(e, "Failed to publish domain events to Kafka");
                throw e;
            }

        }
        private IProducer<string, string> GetProducer(CancellationToken c)
        {
            using (_asyncLock.Wait(c))
            {
                if (_kafkaProducer == null)
                {
                    _kafkaProducer = _producerFactory.CreateProducer();
                }
                return _kafkaProducer;
            }
        }

        private Task ProduceMessages(IReadOnlyCollection<KafkaMessage> kafkaMessages, CancellationToken c)
        {
            var kafkaProducer = GetProducer(c);

            _log.Verbose(
                    "Publishing {0} domain events to Kafka brokers '{1}'",
                    kafkaMessages.Count, _configuration.BootstrapServers);

            foreach (var message in kafkaMessages)
            {
                var headers = new Headers();
                foreach (var item in message.Metadata)
                {
                    headers.Add(item.Key, Encoding.UTF8.GetBytes(item.Value));
                }
                var kafkaDomainMessage = new Message<string, string>
                {
                    Value = message.Message,
                    Headers = headers,
                    Key = message.MessageId.Value
                };
                kafkaProducer.Produce(message.TopicPartition, kafkaDomainMessage);
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_kafkaProducer != null)
            {
                _kafkaProducer.Flush(TimeSpan.FromSeconds(10));
                _kafkaProducer.Dispose();
                _kafkaProducer = null;
            }
        }
    }
}