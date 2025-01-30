using Axis.POC.Services;
using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.AspNetCore.Mvc;

namespace Axis.POC.Controllers
{
    [ApiController]
    [Route("api/videostream")]
    public class VideoStreamController : ControllerBase
    {
        private readonly ILogger<VideoStreamController> _logger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly CameraService _cameraService;

        public VideoStreamController(ILogger<VideoStreamController> logger, IHttpClientFactory clientFactory, CameraService cameraService)
        {
            _logger = logger;
            _clientFactory = clientFactory;
            _cameraService = cameraService;
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
                return File(stream, "multipart/x-mixed-replace; boundary=--myboundary");
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
                var client = _clientFactory.CreateClient();
                var response = await client.GetAsync(cgiUrl, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Failed to connect to the camera");
                }

                var stream = await response.Content.ReadAsStreamAsync();

                var outputPath = Path.Combine(Path.GetTempPath(), $"camera_{cameraId}.m3u8");
                await FFMpegArguments
                    .FromPipeInput(new StreamPipeSource(stream))
                    .OutputToFile(outputPath, true, options => options
                        .WithVideoCodec("libx264")
                        .WithConstantRateFactor(23)
                        .WithVariableBitrate(4)
                        .WithAudioCodec("aac")
                        .WithAudioBitrate(128)
                        .ForceFormat("hls")
                        .WithCustomArgument("-hls_time 4")
                        .WithCustomArgument("-hls_playlist_type event"))
                    .ProcessAsynchronously();

                var fileStream = System.IO.File.OpenRead(outputPath);
                return File(fileStream, "application/vnd.apple.mpegurl", enableRangeProcessing: true);
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

        [HttpGet("cameras")]
        public IActionResult GetCameraList()
        {
            return Ok(_cameraService.GetAllCameras());
        }
    }
}
