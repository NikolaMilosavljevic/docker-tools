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
        public async Task AnnotateEolDigestsCommand_ExecuteAsync()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            EolAnnotationsData eolAnnotations = new()
            {
                EolDate = new DateOnly(2022, 1, 1),
                EolDigests = new List<EolDigestData>
                {
                    new EolDigestData { Digest = "digest1" },
                    new EolDigestData { Digest = "digest2" }
                }
            };

            string eolAnnotationsJson = JsonConvert.SerializeObject(eolAnnotations);
            string eolDigestsListPath = Path.Combine(tempFolderContext.Path, "eol-digests.json");
            File.WriteAllText(eolDigestsListPath, eolAnnotationsJson);

            Mock<IDockerService> dockerServiceMock = new();
            Mock<ILoggerService> loggerServiceMock = new();
            Mock<IProcessService> processServiceMock = new();

            AnnotateEolDigestsCommand command = new(
                dockerServiceMock.Object,
                loggerServiceMock.Object,
                processServiceMock.Object,
                Mock.Of<IRegistryCredentialsProvider>());
            command.Options.EolDigestsListPath = eolDigestsListPath;
            command.Options.Subscription = "subscription";
            command.Options.ResourceGroup = "resource group";
            command.Options.NoCheck = true;

            await command.ExecuteAsync();

            loggerServiceMock.Verify(o => o.WriteMessage("Annotating EOL for digest 'digest1'"));
            loggerServiceMock.Verify(o => o.WriteMessage("Annotating EOL for digest 'digest2'"));
        }
    }
}
