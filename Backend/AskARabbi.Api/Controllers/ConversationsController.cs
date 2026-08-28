using AskARabbi.Api.Authentication;
using AskARabbi.Api.Contracts.Conversations;
using AskARabbiLIB.Conversations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AskARabbi.Api.Controllers;

/// <summary>Manages canonical saved conversation context for the authenticated account.</summary>
[ApiController]
[Authorize]
[Route("api/conversations")]
public sealed class ConversationsController : ControllerBase
{
    private readonly ConversationService conversations;
    private readonly ICurrentUser currentUser;

    /// <summary>Initializes the conversations API.</summary>
    /// <param name="conversations">Conversation application service.</param>
    /// <param name="currentUser">Current authenticated user accessor.</param>
    public ConversationsController(ConversationService conversations, ICurrentUser currentUser)
    {
        this.conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
        this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    /// <summary>Lists recent conversations for the right-hand or left-hand navigation.</summary>
    /// <param name="limit">Maximum summaries to return.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Recent conversation summaries.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ConversationSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationSummaryResponse>>> List([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var values = await conversations.ListAsync(currentUser.UserId, limit, cancellationToken).ConfigureAwait(false);
        return Ok(values.Select(ConversationContractMapper.ToResponse).ToArray());
    }

    /// <summary>Creates a new empty saved conversation.</summary>
    /// <param name="request">Conversation creation request.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The created canonical conversation.</returns>
    [HttpPost]
    [ProducesResponseType<ConversationResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ConversationResponse>> Create(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var conversation = await conversations.CreateAsync(currentUser.UserId, request.Title, request.EnabledSourceKeys, cancellationToken).ConfigureAwait(false);
        var response = ConversationContractMapper.ToResponse(conversation);
        return CreatedAtAction(nameof(Get), new { conversationId = conversation.Id }, response);
    }

    /// <summary>Loads the complete canonical context for a saved conversation.</summary>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The conversation when owned by the current user.</returns>
    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType<ConversationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationResponse>> Get(Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetAsync(currentUser.UserId, conversationId, cancellationToken).ConfigureAwait(false);
        return conversation is null ? NotFound() : Ok(ConversationContractMapper.ToResponse(conversation));
    }

    /// <summary>Appends one user message idempotently and returns the server-owned context.</summary>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="request">New user message.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The canonical conversation after storing the user turn.</returns>
    [HttpPost("{conversationId:guid}/messages")]
    [ProducesResponseType<ConversationTurnResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationTurnResponse>> AppendMessage(Guid conversationId, AppendMessageRequest request, CancellationToken cancellationToken)
    {
        var conversation = await conversations.AppendUserMessageAsync(currentUser.UserId, conversationId, request.MessageId, request.Content, cancellationToken).ConfigureAwait(false);
        return conversation is null ? NotFound() : Ok(new ConversationTurnResponse("stored", ConversationContractMapper.ToResponse(conversation)));
    }

    /// <summary>Renames a saved conversation.</summary>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="request">New title.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>An empty success response when updated.</returns>
    [HttpPut("{conversationId:guid}/title")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rename(Guid conversationId, RenameConversationRequest request, CancellationToken cancellationToken)
    {
        var updated = await conversations.RenameAsync(currentUser.UserId, conversationId, request.Title, cancellationToken).ConfigureAwait(false);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Replaces the source collections enabled for a conversation.</summary>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="request">New source selection.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>An empty success response when updated.</returns>
    [HttpPut("{conversationId:guid}/sources")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSources(Guid conversationId, UpdateConversationSourcesRequest request, CancellationToken cancellationToken)
    {
        var updated = await conversations.UpdateSourcesAsync(currentUser.UserId, conversationId, request.EnabledSourceKeys, cancellationToken).ConfigureAwait(false);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Deletes a saved conversation owned by the current user.</summary>
    /// <param name="conversationId">Conversation ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>An empty success response when deleted.</returns>
    [HttpDelete("{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid conversationId, CancellationToken cancellationToken)
    {
        var deleted = await conversations.DeleteAsync(currentUser.UserId, conversationId, cancellationToken).ConfigureAwait(false);
        return deleted ? NoContent() : NotFound();
    }
}
