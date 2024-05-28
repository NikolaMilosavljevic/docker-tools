// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using static System.Runtime.InteropServices.JavaScript.JSType;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class AnnotateEolDigestsCommand : DockerRegistryCommand<AnnotateEolDigestsOptions, AnnotateEolDigestsOptionsBuilder>
    {
        private readonly IDockerService _dockerService;
        private readonly ILoggerService _loggerService;
        private readonly IProcessService _processService;

        [ImportingConstructor]
        public AnnotateEolDigestsCommand(
            IDockerService dockerService,
            ILoggerService loggerService,
            IProcessService processService,
            IRegistryCredentialsProvider registryCredentialsProvider)
            : base(registryCredentialsProvider)
        {
            _dockerService = new DockerServiceCache(dockerService ?? throw new ArgumentNullException(nameof(dockerService)));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        }

        protected override string Description => "Annotates EOL digests in Docker Registry";

        public override async Task ExecuteAsync()
        {
            if (Options.EolDigestsListPath == null)
            {
                throw new ArgumentNullException("EolDigestsListPath is required.");
            }

            RegistryCredentials? credentials = await RegistryCredentialsProvider.GetCredentialsAsync(
                Manifest.Registry, Manifest.Registry, Options.CredentialsOptions) ?? throw new InvalidOperationException("No credentials found for the registry.");

            await ExecuteWithSuppliedCredentialsAsync(
                Options.IsDryRun,
                async () =>
                {
                    await OrasLogin(credentials);

                },
                credentials,
                registryName: Manifest.Registry);

            // await OrasLogin();

            string testDigest = "dotnetdockerdev.azurecr.io/nikolam/test/alpine-3.19@sha256:6457d53fb065d6f250e1504b9bc42d5b6c65941d57532c072d929dd0628977d0";
            string dateString = DateTime.Today.ToString("yyyy-MM-dd");

            AnnotateDigest(dateString, testDigest);
            WriteSomething();
        }

        private async Task OrasLogin(RegistryCredentials credentials)
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
                Options.IsDryRun);

        }

        private void AnnotateDigest(string date, string digest)
        {
            ExecuteHelper.ExecuteWithRetry(
                "oras",
                $"attach --artifact-type application/vnd.microsoft.artifact.lifecycle --annotation \"vnd.microsoft.artifact.lifecycle.end-of-life.date={date}\" {digest}",
                Options.IsDryRun);
        }

        protected async Task ExecuteWithSuppliedCredentialsAsync(bool isDryRun, Func<Task> action, RegistryCredentials? credentials, string registryName)
        {
            bool loggedIn = false;


            if (!string.IsNullOrEmpty(registryName) && credentials is not null)
            {
                DockerHelper.Login(credentials, registryName, isDryRun);
                loggedIn = true;
            }

            try
            {
                await action();
            }
            finally
            {
                if (loggedIn && !string.IsNullOrEmpty(registryName))
                {
                    DockerHelper.Logout(registryName, isDryRun);
                }
            }
        }

        private void WriteSomething()
        {
            _loggerService.WriteHeading("Annotations - heading");

            _loggerService.WriteMessage("Some notes about annotations applied.");

            _loggerService.WriteMessage();
        }
    }
}
#nullable restore
