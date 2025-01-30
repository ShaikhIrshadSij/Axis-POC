using System.Collections.Concurrent;

namespace Axis.POC.Services
{
    public class CameraService
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _cameraLocks = new();
        private readonly Dictionary<string, string> _cameraUrls = new();

        public CameraService()
        {
            // Initialize camera URLs (you might want to load these from a configuration file or database)
            _cameraUrls.Add("camera1", "http://camera1-ip-address/cgi-bin/stream.cgi");
            _cameraUrls.Add("camera2", "http://camera2-ip-address/cgi-bin/stream.cgi");
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
