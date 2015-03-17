﻿// The MIT License (MIT)
//
// Copyright (c) 2015 EventFlow
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

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace EventFlow
{
    public class MssqlConnection : IMssqlConnection
    {
        public Task<int> ExecuteAsync(string sql, object param = null)
        {
            return WithConnectionAsync(c => c.ExecuteAsync(sql, param));
        }

        public async Task<IReadOnlyCollection<TResult>> QueryAsync<TResult>(string sql, object param = null)
        {
            return (await WithConnectionAsync(c => c.QueryAsync<TResult>(sql, param))).ToList();
        }

        public async Task<TResult> WithConnectionAsync<TResult>(
            Func<IDbConnection, Task<TResult>> withConnection)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["sql.connectionstring"].ConnectionString;
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                await sqlConnection.OpenAsync().ConfigureAwait(false);
                return await withConnection(sqlConnection).ConfigureAwait(false);
            }
        }
    }
}
