using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Source for this extension: https://gist.github.com/ChristopherHaws/b1c54b95838f1513bfb74fa1c8e408f3
/// </summary>
namespace Microsoft.EntityFrameworkCore
{
    public static class AzureSqlServerConnectionExtensions
    {
        public static void UseAzureAccessToken(this DbContextOptionsBuilder options)
        {
            options.ReplaceService<ISqlServerConnection, AzureSqlServerConnection>();
        }
    }

    public class AzureSqlServerConnection : SqlServerConnection
    {
        // Compensate for slow SQL Server database creation
        private const int DefaultMasterConnectionCommandTimeout = 60;

        public AzureSqlServerConnection(RelationalConnectionDependencies dependencies)
            : base(dependencies)
        {
        }

        protected override DbConnection CreateDbConnection() => new SqlConnection(this.ConnectionString)
        {
            // AzureServiceTokenProvider handles caching the token and refreshing it before it expires
            AccessToken = AsyncUtil.RunSync(() => new AzureServiceTokenProvider().GetAccessTokenAsync("https://database.windows.net/"))
        };

        public override ISqlServerConnection CreateMasterConnection()
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(this.ConnectionString)
            {
                InitialCatalog = "master"
            };
            connectionStringBuilder.Remove("AttachDBFilename");

            var contextOptions = new DbContextOptionsBuilder()
                .UseSqlServer(
                    connectionStringBuilder.ConnectionString,
                    b => b.CommandTimeout(this.CommandTimeout ?? DefaultMasterConnectionCommandTimeout))
                .Options;

            return new AzureSqlServerConnection(this.Dependencies.With(contextOptions));
        }
    }


    /// <summary>
    /// Source for this class: https://www.ryadel.com/en/asyncutil-c-helper-class-async-method-sync-result-wait/
    /// </summary>
    public static class AsyncUtil
    {
        private static readonly TaskFactory _taskFactory = new
            TaskFactory(CancellationToken.None,
                        TaskCreationOptions.None,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default);

        /// <summary>
        /// Executes an async Task method which has a void return value synchronously
        /// USAGE: AsyncUtil.RunSync(() => AsyncMethod());
        /// </summary>
        /// <param name="task">Task method to execute</param>
        public static void RunSync(Func<Task> task)
            => _taskFactory
                .StartNew(task)
                .Unwrap()
                .GetAwaiter()
                .GetResult();

        /// <summary>
        /// Executes an async Task<T> method which has a T return type synchronously
        /// USAGE: T result = AsyncUtil.RunSync(() => AsyncMethod<T>());
        /// </summary>
        /// <typeparam name="TResult">Return Type</typeparam>
        /// <param name="task">Task<T> method to execute</param>
        /// <returns></returns>
        public static TResult RunSync<TResult>(Func<Task<TResult>> task)
            => _taskFactory
                .StartNew(task)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
    }
}