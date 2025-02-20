using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace Axis.POC.Services
{
    public class CameraService
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _cameraLocks = new();
        private readonly Dictionary<string, string> _cameraUrls = new();

        public CameraService(IWebHostEnvironment webHostEnvironment)
        {
            // Initialize camera URLs (you might want to load these from a configuration file or database)
            //_cameraUrls.Add("camera4", "http://80.235.76.111:18081/mjpg/video.mjpg");
            //_cameraUrls.Add("camera1", "http://83.56.31.69/mjpg/video.mjpg");
            _cameraUrls.Add("camera2", "http://220.233.144.165:8888/mjpg/video.mjpg");
            //_cameraUrls.Add("camera3", "https://netcam3.kstest01.corp/axis-cgi/mjpg/video.cgi?fps=20&resolution=320X240&compression=10");
            //_cameraUrls = JsonConvert.DeserializeObject<Dictionary<string, string>>(System.IO.File.ReadAllText($"{webHostEnvironment.WebRootPath}/cameras.json"));
            // Add more cameras as needed
        }

        public async Task<bool> TryAcquireCameraLock(string cameraId)
        {
            var semaphore = _cameraLocks.GetOrAdd(cameraId, _ => new SemaphoreSlim(1, 1));
            return await semaphore.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void ReleaseCameraLock(string cameraId)
        {
            if (_cameraLocks.TryGetValue(cameraId, out var semaphore))
            {
                semaphore.Release();
            }
        }

        public string GetCameraUrlById(string cameraId)
        {
            if (_cameraUrls.TryGetValue(cameraId, out var url))
            {
                return url;
            }
            throw new KeyNotFoundException($"Camera with ID {cameraId} not found");
        }

        public IEnumerable<string> GetAllCameras()
        {
            return _cameraUrls.Keys;
        }
    }

}
