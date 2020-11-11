using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Azure.Identity;
using Azure.Core;
using System;

namespace Shared
{
    public class AzureIdentitySqlAuthenticationProvider : SqlAuthenticationProvider
    {
        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            Console.WriteLine($"Sql Auth UserId={parameters.UserId}");
            TokenCredential tokenCredential;
            if (parameters.UserId == "LOCALDEV")
            {
                tokenCredential = new InteractiveBrowserCredential();
            }
            /*
            else if (!string.IsNullOrEmpty(parameters.UserId))
            {
                tokenCredential = new ManagedIdentityCredential(parameters.UserId);
            }
            */
            else
            {
                tokenCredential = new DefaultAzureCredential();
            }
            var token = await tokenCredential.GetTokenAsync(new TokenRequestContext(new[] { "https://database.windows.net/.default" }), default);
            var sqlToken = new SqlAuthenticationToken(token.Token, token.ExpiresOn);
            return sqlToken;
        }

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
        {
            return true;
        }
    }
}
