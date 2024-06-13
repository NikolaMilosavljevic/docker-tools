﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public interface IOrasService
    {
        Task OrasLogin(RegistryCredentials credentials, bool isDryRun);

        bool IsDigestAnnotatedForEol(string digest, bool isDryRun);

        bool AnnotateEolDigest(string digest, DateOnly date, ILoggerService loggerService, bool isDryRun);
    }
}
#nullable disable
