using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ERP.Domain.Interfaces;

namespace ERP.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VoiceController : ControllerBase
{
    private readonly ITranscriptionService _transcriptionService;
    private readonly ChatController _chatController;

    public VoiceController(ITranscriptionService transcriptionService, ChatController chatController)
    {
        _transcriptionService = transcriptionService;
        _chatController = chatController;
    }

    [HttpPost]
    public async Task<IActionResult> TranscribeAndChat(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Audio file cannot be empty.");
        }

        string transcript;
        try
        {
            using var stream = file.OpenReadStream();
            transcript = await _transcriptionService.TranscribeAsync(stream, file.FileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Transcription failed.", details = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return BadRequest("Audio contains no recognizable speech.");
        }

        // Set the controller context of the ChatController to reuse user claims and HttpContext
        _chatController.ControllerContext = this.ControllerContext;

        var chatResult = await _chatController.Chat(new ChatController.ChatRequest { Prompt = transcript });

        object? chatResponseData = null;
        if (chatResult is OkObjectResult okResult)
        {
            chatResponseData = okResult.Value;
        }
        else if (chatResult is AcceptedResult acceptedResult)
        {
            chatResponseData = acceptedResult.Value;
        }
        else if (chatResult is BadRequestObjectResult badRequestResult)
        {
            chatResponseData = badRequestResult.Value;
        }
        else if (chatResult is ObjectResult objectResult)
        {
            chatResponseData = objectResult.Value;
        }

        return Ok(new
        {
            transcript = transcript,
            chatResponse = chatResponseData
        });
    }
}
