// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class AnnotateEolDigestsOptions : DockerRegistryOptions
    {
        public string EolDigestsListPath { get; set; } = string.Empty;
        public DateTime? EolDate { get; set; } = DateTime.UtcNow;
        public string? Subscription { get; set; }
        public string? ResourceGroup { get; set; }

        public AnnotateEolDigestsOptions() : base()
        {
        }
    }

    public class AnnotateEolDigestsOptionsBuilder : DockerRegistryOptionsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        CreateOption<string>("acr-subscription", nameof(BuildOptions.Subscription),
                            "Azure subscription to operate on"),
                        CreateOption<string>("acr-resource-group", nameof(BuildOptions.ResourceGroup),
                            "Azure resource group to operate on"),
                        CreateOption<DateTime?>("eol-date", nameof(AnnotateEolDigestsOptions.EolDate),
                            "EOL annotations date (default is today's date)")
                    }
                );

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(AnnotateEolDigestsOptions.EolDigestsListPath),
                            "Eol annotations digests list path")
                    }
                );
    }
}
#nullable disable
