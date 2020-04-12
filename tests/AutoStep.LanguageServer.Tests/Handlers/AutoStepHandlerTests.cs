using AutoStep.Elements.Test;
using AutoStep.Language;
using AutoStep.Language.Test;
using AutoStep.Projects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AutoStep.LanguageServer.Tests
{
    public class AutoStepHandlerTests
    {
        [Fact]
        public async Task FeatureSetRequestReturnsFeature()
        {
            var mockedHost = new Mock<IProjectHost>();

            var file1 = new ProjectTestFile("/test1", new StringContentSource(""));
            file1.UpdateLastCompileResult(new FileCompilerResult(true, new FileElement
            {
                Feature = new FeatureElement
                {
                    Name = "Feature 1"
                }
            }));

            var file2 = new ProjectTestFile("/test2", new StringContentSource(""));
            file2.UpdateLastCompileResult(new FileCompilerResult(true, new FileElement
            {
                Feature = new FeatureElement
                {
                    Name = "Feature 2"
                }
            }));

            mockedHost.Setup(x => x.GetProjectFilesOfType<ProjectTestFile>()).Returns(new[] {
                file1,
                file2
            });

            var handler = new AutoStepHandler(mockedHost.Object, NullLoggerFactory.Instance);

            var result = await handler.Handle(new FeatureRequest(), CancellationToken.None);

            result.Features.Should().BeEquivalentTo(new FeatureInfo
            {
                Name = "Feature 1",
                SourceFile = "/test1"                
            }, new FeatureInfo
            {
                Name = "Feature 2",
                SourceFile = "/test2"
            });
        }
    }
}
