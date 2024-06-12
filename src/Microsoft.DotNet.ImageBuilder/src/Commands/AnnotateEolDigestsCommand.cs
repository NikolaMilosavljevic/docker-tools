// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.EolAnnotations;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class AnnotateEolDigestsCommand : DockerRegistryCommand<AnnotateEolDigestsOptions, AnnotateEolDigestsOptionsBuilder>
    {
        private readonly IDockerService _dockerService;
        private readonly ILoggerService _loggerService;
        private readonly IProcessService _processService;

        private ConcurrentBag<EolDigestData> _failedAnnotations = new ();

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
            EolAnnotationsData eolAnnotations = LoadEolAnnotationsData(Options.EolDigestsListPath);
            DateOnly eolDate = eolAnnotations.EolDate;

            RegistryCredentials? credentials = await RegistryCredentialsProvider.GetCredentialsAsync(
                Manifest.Registry, Manifest.Registry, Options.CredentialsOptions) ?? throw new InvalidOperationException("No credentials found for the registry.");

            await ExecuteWithSuppliedCredentialsAsync(
                Options.IsDryRun,
                async () =>
                {
                    await OrasLogin(credentials);

                    Parallel.ForEach(eolAnnotations.EolDigests, (a) =>
                    {
                        if (Options.NoCheck || !IsDigestAnnotatedForEol(a.Digest))
                        {
                            _loggerService.WriteMessage($"Annotating EOL for digest '{a.Digest}'");
                            AnnotateEolDigest(a.Digest, a.EolDate ?? eolDate);
                        }
                        else
                        {
                            _loggerService.WriteMessage($"Digest '{a.Digest}' is already annotated for EOL");
                        }
                    });

                },
                credentials,
                registryName: Manifest.Registry);

            if (_failedAnnotations.Count > 0)
            {
                _loggerService.WriteMessage("JSon file for rerunning failed annotations:");
                _loggerService.WriteMessage("");
                _loggerService.WriteMessage(JsonConvert.SerializeObject(new EolAnnotationsData(DateOnly.FromDateTime(DateTime.Today), [.. _failedAnnotations])));
                _loggerService.WriteMessage("");
                throw new InvalidOperationException($"Failed to annotate {_failedAnnotations.Count} digests for EOL.");
            }
        }

        private static EolAnnotationsData LoadEolAnnotationsData(string eolDigestsListPath)
        {
            if (eolDigestsListPath == null)
            {
                throw new ArgumentNullException("EolDigestsListPath is required.");
            }

            string eolAnnotationsJson = File.ReadAllText(eolDigestsListPath);
            EolAnnotationsData? eolAnnotations = JsonConvert.DeserializeObject<EolAnnotationsData>(eolAnnotationsJson);
            return eolAnnotations is null
                ? throw new JsonException($"Unable to correctly deserialize path '{eolAnnotationsJson}'.")
                : eolAnnotations;
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

        private bool IsDigestAnnotatedForEol(string digest)
        {
            string? stdOut = ExecuteHelper.ExecuteWithRetry(
                "oras",
                $"discover --artifact-type application/vnd.microsoft.artifact.lifecycle {digest}",
                Options.IsDryRun);

            if (!string.IsNullOrEmpty(stdOut) && stdOut.Contains("Discovered 0 artifact"))
            {
                return false;
            }

            return true;
        }

        private void AnnotateEolDigest(string digest, DateOnly date)
        {
            try
            {
                ExecuteHelper.ExecuteWithRetry(
                    "oras",
                    $"attach --artifact-type application/vnd.microsoft.artifact.lifecycle --annotation \"vnd.microsoft.artifact.lifecycle.end-of-life.date={date}\" {digest}",
                    Options.IsDryRun);
            }
            catch (InvalidOperationException ex)
            {
                // We do not want to fail immediatelly if one annotation command fails.
                // We will capture all failures and log the json data at the end.
                // Json data can be used to rerun the failed annotations.
                _failedAnnotations.Add(new EolDigestData { Digest = digest, EolDate = date });
                _loggerService.WriteMessage($"Failed to annotate EOL for digest '{digest}': {ex.Message}");
            }
        }

        protected static async Task ExecuteWithSuppliedCredentialsAsync(bool isDryRun, Func<Task> action, RegistryCredentials? credentials, string registryName)
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
    }
}
#nullable restore
