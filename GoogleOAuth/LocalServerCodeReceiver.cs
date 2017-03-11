﻿/*
Copyright 2013 Google Inc

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Logging;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;

namespace Google.Apis.Auth.OAuth2
{
    /// <summary>
    /// OAuth 2.0 verification code receiver that runs a local server on a free port and waits for a call with the 
    /// authorization verification code.
    /// </summary>
    public class LocalServerCodeReceiver : ICodeReceiver
    {
        private static readonly ILogger Logger = ApplicationContext.Logger.ForType<LocalServerCodeReceiver>();

        /// <summary>The call back format. Expects one port parameter.</summary>
        private const string LoopbackCallback = "http://localhost:{0}/authorize/";

        /// <summary>Close HTML tag to return the browser so it will close itself.</summary>
        private const string ClosePageResponse =
@"<html>
  <head><title>OAuth 2.0 Authentication Token Received</title></head>
  <body>
    Received verification code. You may now close this window.
    <script type='text/javascript'>
      // This doesn't work on every browser.
      window.setTimeout(function() {
          window.open('', '_self', ''); 
          window.close(); 
        }, 1000);
      if (window.opener) { window.opener.checkToken(); }
    </script>
  </body>
</html>";

        private string redirectUri;
        public string RedirectUri
        {
            get
            {
                if (!string.IsNullOrEmpty(redirectUri))
                {
                    return redirectUri;
                }

                return redirectUri = string.Format(LoopbackCallback, GetRandomUnusedPort());
            }
        }

        public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl url,
            CancellationToken taskCancellationToken)
        {
            var l = new Microsoft.Net.Http.Server.WebListener();

            //l.BaseAddress = new System.Uri(redirectUri);

            var authorizationUrl = url.Build().ToString();
            //using (var listener = new HttpListener())
            //using (var listener = new System.Net.Http.HttpClient())

            //new Microsoft.Net.Http.Server.WebListenerSettings().UrlPrefixes.Add(RedirectUri);
            
            using (var listener = new Microsoft.Net.Http.Server.WebListener())
            {
                //listener.Prefixes.Add(RedirectUri);
                //listener = new System.Uri(redirectUri);
                listener.Settings.UrlPrefixes.Add(RedirectUri);
                try
                {
                    listener.Start();
                    

                    Logger.Debug("Open a browser with \"{0}\" URL", authorizationUrl);
                    //Process.Start(authorizationUrl);

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        authorizationUrl = authorizationUrl.Replace("&", "^&");
                        Process.Start(new ProcessStartInfo("cmd", $"/c start {authorizationUrl}") { CreateNoWindow = true });
                    }

                    // Wait to get the authorization code response.
                    //var context = await listener..GetContextAsync().ConfigureAwait(false);
                    //NameValueCollection coll = context.Request.QueryString;
                    var context = await listener.AcceptAsync().ConfigureAwait(false);
                    //NameValueCollection coll = context.Request.QueryString;
                    string coll = context.Request.QueryString;

                    // Write a "close" response.
                    //using (var writer = new StreamWriter(context.Response.OutputStream))
                    using (var writer = new StreamWriter(context.Response.Body))
                    {
                        writer.WriteLine(ClosePageResponse);
                        writer.Flush();
                    }
                    //context.Response.OutputStream.Close();
                    //context.Response

                    coll = coll.Remove(0,1);
                    var pairs = coll.Split('&');
                    var queryString = new Dictionary<string, string>();
                    foreach (var pair in pairs)
                    {
                        var keyValue = pair.Split('=');
                        queryString[keyValue[0]] = keyValue[1];
                    }

                    return new AuthorizationCodeResponseUrl(queryString);
                    // Create a new response URL with a dictionary that contains all the response query parameters.
                    //return new AuthorizationCodeResponseUrl(coll.AllKeys.ToDictionary(k => k, k => coll[k]));

                    
                }
                finally
                {
                    listener.Dispose();
                }
            }
        }


        /// <summary>Returns a random, unused port.</summary>
        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
