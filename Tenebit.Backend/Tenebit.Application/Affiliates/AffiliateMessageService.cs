using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Affiliates;

public sealed record AffiliateMessageResponse(Guid Id, string SenderType, string Body, DateTimeOffset SentAt);

public sealed record AffiliateMessageThreadResponse(
    Guid Id, string Subject, string Category, string Status, DateTimeOffset LastMessageAt,
    bool UnreadByAdmin, bool UnreadByAffiliate, IReadOnlyList<AffiliateMessageResponse> Messages);

public sealed record AffiliateMessageThreadSummary(
    Guid Id, Guid AffiliateId, string Subject, string Category, string Status, DateTimeOffset LastMessageAt, bool UnreadByAdmin);

/// <summary>
/// The two-way "some contact form that reaches me and I can answer back" from the brief (spec §8) - a
/// thread per topic rather than a single mailbox, so a payout question and a formal grievance
/// ("Zgłoś zastrzeżenie" in the partner panel) show up as visibly different things in the admin inbox
/// without needing a separate ticketing system.
/// </summary>
public sealed class AffiliateMessageService
{
    private readonly IAffiliateMessageThreadRepository _threads;
    private readonly IAffiliateMessageRepository _messages;
    private readonly IAffiliateRepository _affiliates;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AffiliateMessageService(IAffiliateMessageThreadRepository threads, IAffiliateMessageRepository messages, IAffiliateRepository affiliates, IUnitOfWork unitOfWork, IClock clock)
    {
        _threads = threads;
        _messages = messages;
        _affiliates = affiliates;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AffiliateMessageThreadSummary>> ListForAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        (await _threads.ListByAffiliateAsync(affiliateId, cancellationToken)).Select(ToSummary).ToList();

    public async Task<IReadOnlyList<AffiliateMessageThreadSummary>> ListForAdminAsync(CancellationToken cancellationToken) =>
        (await _threads.ListAllAsync(cancellationToken)).Select(ToSummary).ToList();

    public Task<int> CountUnreadForAdminAsync(CancellationToken cancellationToken) => _threads.CountUnreadByAdminAsync(cancellationToken);

    public async Task<Result<AffiliateMessageThreadResponse>> GetThreadForAffiliateAsync(Guid affiliateId, Guid threadId, CancellationToken cancellationToken)
    {
        var thread = await _threads.GetByIdAsync(threadId, cancellationToken);
        if (thread is null || thread.AffiliateId != affiliateId) return Result<AffiliateMessageThreadResponse>.Failure(Error.NotFound("Wątek nie istnieje."));

        thread.MarkReadByAffiliate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AffiliateMessageThreadResponse>.Success(await ToDetailAsync(thread, cancellationToken));
    }

    public async Task<Result<AffiliateMessageThreadResponse>> GetThreadForAdminAsync(Guid threadId, CancellationToken cancellationToken)
    {
        var thread = await _threads.GetByIdAsync(threadId, cancellationToken);
        if (thread is null) return Result<AffiliateMessageThreadResponse>.Failure(Error.NotFound("Wątek nie istnieje."));

        thread.MarkReadByAdmin();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AffiliateMessageThreadResponse>.Success(await ToDetailAsync(thread, cancellationToken));
    }

    public async Task<Result<AffiliateMessageThreadResponse>> CreateThreadAsync(Guid affiliateId, string subject, bool isComplaint, string body, CancellationToken cancellationToken)
    {
        var affiliate = await _affiliates.GetByIdAsync(affiliateId, cancellationToken);
        if (affiliate is null) return Result<AffiliateMessageThreadResponse>.Failure(Error.NotFound("Konto partnerskie nie istnieje."));

        try
        {
            var now = _clock.UtcNow;
            var category = isComplaint ? AffiliateMessageThreadCategory.Complaint : AffiliateMessageThreadCategory.General;
            var thread = new AffiliateMessageThread(affiliateId, subject, category, now);
            var message = new AffiliateMessage(thread.Id, AffiliateMessageSenderType.Affiliate, body, now);
            _threads.Add(thread);
            _messages.Add(message);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AffiliateMessageThreadResponse>.Success(await ToDetailAsync(thread, cancellationToken));
        }
        catch (Domain.Common.DomainException ex)
        {
            return Result<AffiliateMessageThreadResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result> ReplyAsAffiliateAsync(Guid affiliateId, Guid threadId, string body, CancellationToken cancellationToken)
    {
        var thread = await _threads.GetByIdAsync(threadId, cancellationToken);
        if (thread is null || thread.AffiliateId != affiliateId) return Result.Failure(Error.NotFound("Wątek nie istnieje."));
        return await AppendAsync(thread, AffiliateMessageSenderType.Affiliate, body, cancellationToken);
    }

    public async Task<Result> ReplyAsAdminAsync(Guid threadId, string body, CancellationToken cancellationToken)
    {
        var thread = await _threads.GetByIdAsync(threadId, cancellationToken);
        if (thread is null) return Result.Failure(Error.NotFound("Wątek nie istnieje."));
        return await AppendAsync(thread, AffiliateMessageSenderType.Admin, body, cancellationToken);
    }

    private async Task<Result> AppendAsync(AffiliateMessageThread thread, AffiliateMessageSenderType senderType, string body, CancellationToken cancellationToken)
    {
        try
        {
            var now = _clock.UtcNow;
            _messages.Add(new AffiliateMessage(thread.Id, senderType, body, now));
            if (senderType == AffiliateMessageSenderType.Affiliate) thread.RecordAffiliateReply(now);
            else thread.RecordAdminReply(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Domain.Common.DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }
    }

    private async Task<AffiliateMessageThreadResponse> ToDetailAsync(AffiliateMessageThread thread, CancellationToken cancellationToken)
    {
        var messages = await _messages.ListByThreadAsync(thread.Id, cancellationToken);
        return new AffiliateMessageThreadResponse(
            thread.Id, thread.Subject, thread.Category.ToString(), thread.Status.ToString(), thread.LastMessageAt,
            thread.UnreadByAdmin, thread.UnreadByAffiliate,
            messages.Select(m => new AffiliateMessageResponse(m.Id, m.SenderType.ToString(), m.Body, m.SentAt)).ToList());
    }

    private static AffiliateMessageThreadSummary ToSummary(AffiliateMessageThread thread) => new(
        thread.Id, thread.AffiliateId, thread.Subject, thread.Category.ToString(), thread.Status.ToString(),
        thread.LastMessageAt, thread.UnreadByAdmin);
}
