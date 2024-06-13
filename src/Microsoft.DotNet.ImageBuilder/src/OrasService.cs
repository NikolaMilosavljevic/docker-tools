// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Models.EolAnnotations;
using System.Diagnostics;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IOrasService))]
    public class OrasService : IOrasService
    {

        public async Task OrasLogin(RegistryCredentials credentials, bool isDryRun)
        {
            ProcessStartInfo startInfo = new(
                "oras", $"login dotnetdockerdev.azurecr.io --username {credentials.Username} --password-stdin")
            {
                RedirectStandardInput = true
            };

            ExecuteHelper.ExecuteWithRetry(
                startInfo,
                process =>
                {
                    process.StandardInput.WriteLine(credentials.Password);
                    process.StandardInput.Close();
                },
                isDryRun);
        }

        public bool IsDigestAnnotatedForEol(string digest, bool isDryRun)
        {
            string? stdOut = ExecuteHelper.ExecuteWithRetry(
                "oras",
                $"discover --artifact-type application/vnd.microsoft.artifact.lifecycle {digest}",
                isDryRun);

            if (!string.IsNullOrEmpty(stdOut) && stdOut.Contains("Discovered 0 artifact"))
            {
                return false;
            }

            return true;
        }

        public bool AnnotateEolDigest(string digest, DateOnly date, ILoggerService loggerService, bool isDryRun)
        {
            try
            {
                ExecuteHelper.ExecuteWithRetry(
                    "oras",
                    $"attach --artifact-type application/vnd.microsoft.artifact.lifecycle --annotation \"vnd.microsoft.artifact.lifecycle.end-of-life.date={date}\" {digest}",
                    isDryRun);
            }
            catch (InvalidOperationException ex)
            {
                loggerService.WriteMessage($"Failed to annotate EOL for digest '{digest}': {ex.Message}");
                return false;
            }

            return true;
        }

    }
}
#nullable disable
