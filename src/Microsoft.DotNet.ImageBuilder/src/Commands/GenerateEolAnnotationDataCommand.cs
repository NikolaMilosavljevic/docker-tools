// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.EolAnnotations;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;
using Octokit;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GenerateEolAnnotationDataCommand : ManifestCommand<GenerateEolAnnotationDataOptions, GenerateEolAnnotationDataOptionsBuilder>
    {
        private readonly ILoggerService _loggerService;
        private readonly DateOnly _eolDate;
        private EolAnnotationsData? _eolAnnotations = null;

        private EolAnnotationsData? EolAnnotations
        {
            get
            {
                if (_eolAnnotations == null)
                {
                    _eolAnnotations = new EolAnnotationsData
                    {
                        EolDate = _eolDate,
                        EolDigests = new List<EolDigestData>()
                    };
                }

                return _eolAnnotations;
            }
        }
        [ImportingConstructor]
        public GenerateEolAnnotationDataCommand(
            ILoggerService loggerService)
            : base()
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _eolDate = DateOnly.FromDateTime(DateTime.UtcNow); // default EOL date
        }

        protected override string Description => "Generate EOL annotation data";

        public override async Task ExecuteAsync()
        {
            if (string.IsNullOrEmpty(Options.OldImageInfoPath) ||
                string.IsNullOrEmpty(Options.NewImageInfoPath))
            {
                throw new ArgumentNullException("Image-info paths are required.");
            }

            ImageArtifactDetails oldImageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.OldImageInfoPath, Manifest);
            ImageArtifactDetails newImageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.NewImageInfoPath, Manifest);

            try
            {

                foreach (RepoData oldRepo in oldImageArtifactDetails.Repos)
                {
                    RepoData? newRepo = newImageArtifactDetails.Repos.FirstOrDefault(r => r.Repo == oldRepo.Repo);
                    if (newRepo == null)
                    {
                        // Annotate all images in the old repo as EOL
                        AnnotateRepoEol(oldRepo);
                    }

                    foreach (ImageData oldImage in oldRepo.Images)
                    {
                        // Logic:
                        // For each platform in the old image, check if it exists in the new repo,
                        // where the platform is defined by Dockerfile value.
                        // If it doesn't add for annotation.
                        // If none of the platforms, in this image, exist in new image, annotate the image as EOL

                        ImageData? newImage = null;
                        string oldImageIdentity = ImageIdentityString(oldImage);
                        foreach (PlatformData oldPlatform in oldImage.Platforms)
                        {
                            if (newImage == null)
                            {
                                // There might be more than one image that contains the platform entry for this dockerfile
                                // find the correct one that matches product version and the set of image tags
                                newImage = newRepo!.Images.Where(i => i.Platforms.Any(p => p.Dockerfile == oldPlatform.Dockerfile)).FirstOrDefault(i => ImageIdentityString(i) == oldImageIdentity);
                            }

                            if (newImage == null)
                            {
                                // TODO - annotate the old platform as EOL
                                EolAnnotations.AddDigest(oldPlatform.Digest);
                            }
                            else
                            {
                                PlatformData? newPlatform = newImage!.Platforms.FirstOrDefault(p => p.Dockerfile == oldPlatform.Dockerfile);
                                if (newPlatform == null || oldPlatform.Digest != newPlatform.Digest)
                                {
                                    EolAnnotations.AddDigest(oldPlatform.Digest);
                                }
                            }
                        }

                        // If we didn't find the new image that contained any of the Dockerfiles from the old image,
                        // or if new image manifest digest is different from old image manifest digest,
                        // annotate old image manifest digest - if it exists.
                        if (oldImage.Manifest != null &&
                            (newImage == null ||
                             newImage.Manifest == null ||
                             oldImage.Manifest.Digest != newImage.Manifest.Digest))
                        {
                            EolAnnotations.AddDigest(oldImage.Manifest.Digest);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _loggerService.WriteError($"Error occurred while generating EOL annotation data: {e}");
                throw;
            }

            string annotationsJson = JsonConvert.SerializeObject(EolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            File.WriteAllText(Options.EolDigestsListPath, annotationsJson);
        }

        private void AnnotateRepoEol(RepoData repo)
        {
            foreach (ImageData image in repo.Images)
            {
                if (image.Manifest != null)
                {
                    EolAnnotations!.AddDigest(image.Manifest.Digest);
                }

                foreach (PlatformData platform in image.Platforms)
                {
                    EolAnnotations!.AddDigest(platform.Digest);
                }
            }
        }

        private string ImageIdentityString(ImageData image) =>
            image.ProductVersion + (image.Manifest?.SharedTags != null ? " " + string.Join(" ", image.Manifest.SharedTags.Order()) : "");

        private static EolAnnotationsData Unused_LoadEolAnnotationsData(string eolDigestsListPath)
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
    }
}
#nullable disable
