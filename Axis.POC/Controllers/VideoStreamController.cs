using Axis.POC.Services;
using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO;

namespace Axis.POC.Controllers
{
    [ApiController]
    [Route("api/videostream")]
    public class VideoStreamController : ControllerBase
    {
        private readonly ILogger<VideoStreamController> _logger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly CameraService _cameraService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public VideoStreamController(ILogger<VideoStreamController> logger, IHttpClientFactory clientFactory, CameraService cameraService, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _clientFactory = clientFactory;
            _cameraService = cameraService;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet("{cameraId}/mjpeg")]
        public async Task<IActionResult> GetMjpegStream(string cameraId)
        {
            try
            {
                var cgiUrl = _cameraService.GetCameraUrlById(cameraId);
                var client = _clientFactory.CreateClient();
                var response = await client.GetAsync(cgiUrl, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Failed to connect to the camera");
                }

                var stream = await response.Content.ReadAsStreamAsync();
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";
                Response.Headers["Connection"] = "keep-alive";
                Response.Headers["Content-Type"] = "multipart/x-mixed-replace; boundary=--myboundary";

                return new FileStreamResult(stream, "multipart/x-mixed-replace; boundary=--myboundary");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming MJPEG for camera {CameraId}", cameraId);
                return StatusCode(500, "An error occurred while streaming the video");
            }
        }

        [HttpGet("{cameraId}/full")]
        public async Task<IActionResult> GetFullVideoStream(string cameraId)
        {
            if (!await _cameraService.TryAcquireCameraLock(cameraId))
            {
                return StatusCode(503, "Camera is currently in use");
            }

            try
            {
                var cgiUrl = _cameraService.GetCameraUrlById(cameraId);
                string outputDir = Path.Combine($@"{_webHostEnvironment.WebRootPath}\camera", cameraId);
                Directory.CreateDirectory(outputDir); // Ensure directory exists
                string outputPath = Path.Combine(outputDir, "playlist.m3u8");

                GlobalFFOptions.Configure(options => options.BinaryFolder = _webHostEnvironment.WebRootPath);

                var client = _clientFactory.CreateClient();
                var response = await client.GetAsync(cgiUrl, HttpCompletionOption.ResponseHeadersRead);
                var stream = await response.Content.ReadAsStreamAsync();
                _ = Task.Run(() =>
                {
                    FFMpegArguments
                .FromPipeInput(new StreamPipeSource(stream))
                .OutputToFile(outputPath, overwrite: true, options => options
                    .WithVideoCodec("libx264")
                    .WithConstantRateFactor(23)
                    .WithVariableBitrate(4)
                    .WithAudioCodec("aac")
                    .WithAudioBitrate(128)
                    .ForceFormat("hls")
                    .WithCustomArgument("-hls_time 2") // Set 2-second segment duration
                    .WithCustomArgument("-hls_playlist_type event") // Keep playlist updated dynamically
                    .WithCustomArgument("-hls_flags append_list+delete_segments") // Dynamically append and delete segments
                    .WithCustomArgument("-hls_list_size 10") // Keep the last 10 segments in the playlist
                    .WithCustomArgument($"-hls_segment_filename {Path.Combine(outputDir, "playlist%d.ts")}") // Properly define segment filename
                )
                .ProcessAsynchronously();
                });

                string hlsUrl = $"https://localhost:7293/api/videostream/fullscreen/{cameraId}/playlist.m3u8";
                return Ok(new { url = hlsUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming full video for camera {CameraId}", cameraId);
                return StatusCode(500, "An error occurred while streaming the video");
            }
            finally
            {
                _cameraService.ReleaseCameraLock(cameraId);
            }
        }

        [HttpGet("fullscreen/{cameraId}/{type}")]
        public async Task<IActionResult> GetStreamFullVideoStream(string cameraId, string type)
        {
            var stream = new FileStream($"{_webHostEnvironment.WebRootPath}/camera/{cameraId}/{type}", System.IO.FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return File(stream, "video/MP2T");
        }

        [HttpGet("cameras")]
        public IActionResult GetCameraList()
        {
            return Ok(_cameraService.GetAllCameras());
        }
    }
}
