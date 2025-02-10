using Axis.POC.Services;
using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
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
        private static readonly ConcurrentDictionary<string, byte[]> _cameraFrames = new();

        public VideoStreamController(ILogger<VideoStreamController> logger, IHttpClientFactory clientFactory, CameraService cameraService, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _clientFactory = clientFactory;
            _cameraService = cameraService;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet("UI")]
        public FileResult UI()
        {
            return PhysicalFile($"{_webHostEnvironment.WebRootPath}/UI/browser/index.html", "text/HTML");
        }

        // Dictionary to store cached frames for each camera
        private static readonly ConcurrentDictionary<string, byte[]> _cachedFrames = new ConcurrentDictionary<string, byte[]>();

        // Dictionary to store timers for refreshing frames for each camera
        private static readonly ConcurrentDictionary<string, Timer> _cameraTimers = new ConcurrentDictionary<string, Timer>();

        [HttpGet("{cameraId}/mjpeg")]
        public async Task GetMjpegStream(string cameraId)
        {
            // Set response headers for MJPEG streaming
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            Response.Headers["Connection"] = "keep-alive";
            Response.ContentType = "multipart/x-mixed-replace; boundary=frame";

            // Get the camera URL
            var cgiUrl = _cameraService.GetCameraUrlById(cameraId);
            if (string.IsNullOrEmpty(cgiUrl))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Invalid camera ID.");
                return;
            }

            // Initialize the frame cache and timer if not already done
            if (!_cachedFrames.ContainsKey(cameraId))
            {
                await InitializeCameraStream(cameraId, cgiUrl);
            }

            try
            {
                // Continuously stream frames from the cache
                while (!HttpContext.RequestAborted.IsCancellationRequested)
                {
                    if (_cachedFrames.TryGetValue(cameraId, out var frame))
                    {
                        // Write the boundary and frame to the response
                        await Response.WriteAsync("--frame\r\n");
                        await Response.WriteAsync("Content-Type: image/jpeg\r\n\r\n");
                        await Response.Body.WriteAsync(frame, 0, frame.Length);
                        await Response.WriteAsync("\r\n");

                        // Flush the response to ensure the frame is sent immediately
                        await Response.Body.FlushAsync();
                    }

                    // Delay to control the frame rate (e.g., 2 frames per second)
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming frames from camera {CameraId}", cameraId);
                Response.StatusCode = 500;
                await Response.WriteAsync("Error streaming frames.");
            }
        }

        private async Task InitializeCameraStream(string cameraId, string cgiUrl)
        {
            try
            {
                // Configure FFmpeg
                GlobalFFOptions.Configure(options => options.BinaryFolder = _webHostEnvironment.WebRootPath);

                // Fetch the initial frame
                using var frameStream = new MemoryStream();
                await FFMpegArguments
                    .FromUrlInput(new Uri(cgiUrl), options => options.WithFramerate(20))
                    .OutputToPipe(new StreamPipeSink(frameStream), options => options
                        .WithVideoCodec("mjpeg")
                        .ForceFormat("mjpeg")
                        .WithFrameOutputCount(1))
                    .ProcessAsynchronously();

                frameStream.Seek(0, SeekOrigin.Begin);
                var frame = frameStream.ToArray();
                _cachedFrames[cameraId] = frame;

                // Set up a timer to periodically refresh the frame
                _cameraTimers[cameraId] = new Timer(async _ =>
                {
                    try
                    {
                        using var refreshStream = new MemoryStream();
                        await FFMpegArguments
                            .FromUrlInput(new Uri(cgiUrl), options => options.WithFramerate(20))
                            .OutputToPipe(new StreamPipeSink(refreshStream), options => options
                                .WithVideoCodec("mjpeg")
                                .ForceFormat("mjpeg")
                                .WithFrameOutputCount(1))
                            .ProcessAsynchronously();

                        refreshStream.Seek(0, SeekOrigin.Begin);
                        var refreshedFrame = refreshStream.ToArray();
                        _cachedFrames[cameraId] = refreshedFrame;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error refreshing frame for camera {CameraId}", cameraId);
                    }
                }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing stream for camera {CameraId}", cameraId);
                throw;
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

                string hlsUrl = $"{Request.Scheme}://{Request.Host}/api/videostream/fullscreen/{cameraId}/playlist.m3u8";
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
