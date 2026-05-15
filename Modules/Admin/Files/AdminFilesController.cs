using System.Security.Claims;
using System.Text.Json;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/files")]
[Authorize(Roles = "Admin")]
public class AdminFilesController : ControllerBase
{
    private const string UpstreamUploadUrl = "http://line-ddpm.we-builds.com/drop-in-document/api/Files/upload";
    private readonly IHttpClientFactory _httpClientFactory;

    public AdminFilesController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("upload-image")]
    public async Task<ActionResult<Response<string>>> UploadImage([FromForm] IFormFile? file, CancellationToken ct)
    {
        var adminId = GetAdminId();
        if (adminId == null)
        {
            return Unauthorized(new Response<string> { Status = 401, Message = "ไม่พบผู้ใช้" });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new Response<string> { Status = 400, Message = "ไม่พบไฟล์" });
        }

        var client = _httpClientFactory.CreateClient();
        using var multipart = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
        multipart.Add(fileContent, "file", file.FileName);

        using var upstream = await client.PostAsync(UpstreamUploadUrl, multipart, ct);
        var body = await upstream.Content.ReadAsStringAsync(ct);
        if (!upstream.IsSuccessStatusCode)
        {
            return StatusCode((int)upstream.StatusCode, new Response<string>
            {
                Status = (int)upstream.StatusCode,
                Message = string.IsNullOrWhiteSpace(body) ? "อัปโหลดไม่สำเร็จ" : body
            });
        }

        var uploadedUrl = TryExtractUrl(body);
        if (string.IsNullOrWhiteSpace(uploadedUrl))
        {
            return BadRequest(new Response<string> { Status = 400, Message = "อัปโหลดสำเร็จแต่ไม่พบ URL" });
        }

        return Ok(new Response<string> { Status = 200, Message = "อัปโหลดสำเร็จ", Data = uploadedUrl });
    }

    private static string? TryExtractUrl(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                return doc.RootElement.GetString();
            }

            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                if (data.ValueKind == JsonValueKind.String)
                {
                    return data.GetString();
                }

                if (data.ValueKind == JsonValueKind.Object)
                {
                    if (data.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                    {
                        return urlEl.GetString();
                    }
                    if (data.TryGetProperty("fileUrl", out var fileUrlEl) && fileUrlEl.ValueKind == JsonValueKind.String)
                    {
                        return fileUrlEl.GetString();
                    }
                    if (data.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String)
                    {
                        return pathEl.GetString();
                    }
                }
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("url", out var urlRoot) && urlRoot.ValueKind == JsonValueKind.String)
                {
                    return urlRoot.GetString();
                }
                if (doc.RootElement.TryGetProperty("fileUrl", out var fileUrlRoot) && fileUrlRoot.ValueKind == JsonValueKind.String)
                {
                    return fileUrlRoot.GetString();
                }
                if (doc.RootElement.TryGetProperty("path", out var pathRoot) && pathRoot.ValueKind == JsonValueKind.String)
                {
                    return pathRoot.GetString();
                }
            }
        }
        catch
        {
            // ignore parse error and return null
        }

        return null;
    }

    private int? GetAdminId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
