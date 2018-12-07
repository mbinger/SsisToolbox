using System;
using System.IO;
using SsisToolbox.Interface;

namespace SsisToolbox.Reliability
{
    /// <summary>
    /// Open remote files in local temp storage
    /// </summary>
    public class TempFile : IDisposable
    {
        /// <summary>
        /// </summary>
        /// <param name="remotePath">Remote file path</param>
        /// <param name="localTempStoragePath">Local temp storage path or NULL to use system default</param>
        public TempFile(string remotePath, string localTempStoragePath = null)
        {
            RemotePath = remotePath;
            _tempPath = String.IsNullOrEmpty(localTempStoragePath) ? Path.GetTempPath() : localTempStoragePath;
        }

        public readonly string RemotePath;
        public string LocalPath { get; private set; }
        private readonly string _tempPath;
        private readonly ICircuitBreaker _circuitBreaker = new CircuitBreakerFactory().Create();

        /// <summary>
        /// Copy remote file to local
        /// </summary>
        public void Copy()
        {
            if (String.IsNullOrEmpty(LocalPath))
            {
                _circuitBreaker.Action(() =>
                {
                    var localPath = Path.Combine(_tempPath, Path.GetFileName(RemotePath));
                    File.Copy(RemotePath, localPath, true);
                    LocalPath = localPath;
                    return true;
                });
            }
        }

        public void Dispose()
        {
            if (!String.IsNullOrEmpty(LocalPath))
            {
                try
                {
                    File.Delete(LocalPath);
                    LocalPath = "";
                }
                catch (Exception)
                {
                    //ignore
                }
            }
        }
    }
}
