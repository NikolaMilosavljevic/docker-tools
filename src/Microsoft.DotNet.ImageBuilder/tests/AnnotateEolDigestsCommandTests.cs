// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.EolAnnotations;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.Win32;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestServiceHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class AnnotateEolDigestsCommandTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public AnnotateEolDigestsCommandTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task AnnotateEolDigestsCommand_NotAlreadyAnnotated_Success()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile.custom");
            File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

            Manifest manifest = ManifestHelper.CreateManifest(
                ManifestHelper.CreateRepo("runtime",
                    ManifestHelper.CreateImage(
                        ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { "tag1", "tag2" })))
            );
            manifest.Registry = "mcr.microsoft.com";
            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(Path.Combine(tempFolderContext.Path, "manifest.json"), JsonConvert.SerializeObject(manifest));

            EolAnnotationsData eolAnnotations = new()
            {
                EolDate = new DateOnly(2024, 6, 10),
                EolDigests = new List<EolDigestData>
                {
                    new EolDigestData { Digest = "digest1" },
                    new EolDigestData { Digest = "digest2", EolDate = new DateOnly(2022, 1, 1) }
                }
            };

            string eolAnnotationsJson = JsonConvert.SerializeObject(eolAnnotations);
            string eolDigestsListPath = Path.Combine(tempFolderContext.Path, "eol-digests.json");
            File.WriteAllText(eolDigestsListPath, eolAnnotationsJson);

            Mock<IDockerService> dockerServiceMock = new();
            Mock<ILoggerService> loggerServiceMock = new();
            Mock<IProcessService> processServiceMock = new();
            Mock<IOrasService> orasServiceMock = CreateOrasServiceMock(digestAlreadyAnnotated: false, digestAnnotationSucceeded: true);
            Mock<IRegistryCredentialsProvider> registryCredentialsProviderMock = CreateRegistryCredentialsProviderMock();
            AnnotateEolDigestsCommand command = new(
                dockerServiceMock.Object,
                loggerServiceMock.Object,
                processServiceMock.Object,
                orasServiceMock.Object,
                registryCredentialsProviderMock.Object);
            command.Options.EolDigestsListPath = eolDigestsListPath;
            command.Options.Subscription = "941d4baa-5ef2-462e-b4b1-505791294610";
            command.Options.ResourceGroup = "DotnetContainers";
            command.Options.NoCheck = true;
            command.Options.CredentialsOptions.Credentials.Add("mcr.microsoft.com", new RegistryCredentials("user", "pass"));
            command.Options.Manifest = manifestPath;

            command.LoadManifest();
            await command.ExecuteAsync();

            loggerServiceMock.Verify(o => o.WriteMessage("Annotating EOL for digest 'digest1', date '6/10/2024'"));
            loggerServiceMock.Verify(o => o.WriteMessage("Annotating EOL for digest 'digest2', date '1/1/2022'"));
        }

        private Mock<IRegistryCredentialsProvider> CreateRegistryCredentialsProviderMock()
        {
            Mock<IRegistryCredentialsProvider> registryCredentialsProviderMock = new();
            registryCredentialsProviderMock
                .Setup(o => o.GetCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RegistryCredentialsOptions>()))
                .ReturnsAsync(new RegistryCredentials("username", "password"));

            return registryCredentialsProviderMock;
        }

        private Mock<IOrasService> CreateOrasServiceMock(bool digestAlreadyAnnotated = true, bool digestAnnotationSucceeded = true)
        {
            Mock<IOrasService> orasServiceMock = new();
            orasServiceMock
                .Setup(o => o.IsDigestAnnotatedForEol(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(digestAlreadyAnnotated);

            orasServiceMock
                .Setup(o => o.AnnotateEolDigest(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<ILoggerService>(), It.IsAny<bool>()))
                .Returns(digestAnnotationSucceeded);

            return orasServiceMock;
        }
    }
}
