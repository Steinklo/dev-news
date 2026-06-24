using DevNews.Application.Common.Repositories;
using DevNews.Application.SocialPost.Commands;
using DevNews.Domain.Common;
using DevNews.Domain.SocialPost.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DevNews.UnitTests.Application.SocialPost.Commands;

public class PersistSocialPostHandlerTests
{
    private readonly ISocialPostRepository _repository = Substitute.For<ISocialPostRepository>();
    private readonly PersistSocialPostHandler _handler;

    public PersistSocialPostHandlerTests()
    {
        _handler = new PersistSocialPostHandler(_repository, NullLogger<PersistSocialPostHandler>.Instance);
    }

    [Fact]
    public async Task Handle_Published_MarksPublished()
    {
        DevNews.Domain.SocialPost.SocialPost? captured = null;
        _repository.AddAsync(Arg.Any<DevNews.Domain.SocialPost.SocialPost>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                captured = ci.ArgAt<DevNews.Domain.SocialPost.SocialPost>(0);
                return ResultResponse<DevNews.Domain.SocialPost.SocialPost>.Success(captured);
            });

        var command = new PersistSocialPostCommand(
            Guid.NewGuid(), new string('a', 150), "https://example.com/a", "ext-1", "https://www.linkedin.com/x", Published: true);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SocialPostStatus.Published, captured!.Status);
    }

    [Fact]
    public async Task Handle_NotPublished_MarksFailed()
    {
        DevNews.Domain.SocialPost.SocialPost? captured = null;
        _repository.AddAsync(Arg.Any<DevNews.Domain.SocialPost.SocialPost>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                captured = ci.ArgAt<DevNews.Domain.SocialPost.SocialPost>(0);
                return ResultResponse<DevNews.Domain.SocialPost.SocialPost>.Success(captured);
            });

        var command = new PersistSocialPostCommand(
            Guid.NewGuid(), new string('a', 150), "https://example.com/a", null, null, Published: false);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SocialPostStatus.Failed, captured!.Status);
    }
}
