using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Providers;
using Orleans.Runtime;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Origine.StorageProviders
{
    /// <summary>
    /// Orleans storage provider implementation for file-backed stores.
    /// </summary>
    /// <remarks>
    /// The storage provider should be included in a deployment by adding this line to the Orleans server configuration file:
    /// 
    ///     <Provider Type="Samples.StorageProviders.OrleansFileStorage" Name="FileStore" RooDirectory="SOME FILE PATH" />
    ///
    /// and this line to any grain that uses it:
    /// 
    ///     [StorageProvider(ProviderName = "FileStore")]
    /// 
    /// The name 'FileStore' is an arbitrary choice.
    /// 
    /// Note that unless the root directory path is a network path available to all silos in a deployment, grain state
    /// will not transport from one silo to another.
    /// </remarks>
    public class OrleansFileStorage : BaseJSONStorageProvider
    {
        /// <summary>
        /// The directory path, relative to the host of the silo. Set from
        /// configuration data during initialization.
        /// </summary>
        public string RootDirectory { get; set; }


        public OrleansFileStorage(ILoggerFactory loggerFactory, ITypeResolver typeResolver, IGrainFactory grainFactory) : base(loggerFactory, typeResolver, grainFactory)
        {

        }

        /// <summary>
        /// Initializes the provider during silo startup.
        /// </summary>
        /// <param name="name">The name of this provider instance.</param>
        /// <param name="providerRuntime">A Orleans runtime object managing all storage providers.</param>
        /// <param name="config">Configuration info for this provider instance.</param>
        /// <returns>Completion promise for this operation.</returns>
        public override Task Init(string name, IProviderRuntime providerRuntime, IProviderConfiguration config)
        {
            this.Name = name;
            this.RootDirectory = config.Properties["RootDirectory"];
            if (string.IsNullOrWhiteSpace(RootDirectory)) throw new ArgumentException("RootDirectory property not set");
            DataManager = new GrainStateFileDataManager(RootDirectory);
            return base.Init(name, providerRuntime, config);
        }
    }

    /// <summary>
    /// Interfaces with the file system.
    /// </summary>
    internal class GrainStateFileDataManager : IJSONStateDataManager
    {
        private readonly DirectoryInfo directory;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="storageDirectory">A path relative to the silo host process' working directory.</param>
        public GrainStateFileDataManager(string storageDirectory)
        {
            directory = new DirectoryInfo(storageDirectory);
            if (!directory.Exists)
                directory.Create();
        }

        /// <summary>
        /// Deletes a file representing a grain state object.
        /// </summary>
        /// <param name="collectionName">The type of the grain state object.</param>
        /// <param name="key">The grain id string.</param>
        /// <returns>Completion promise for this operation.</returns>
        public Task Delete(string collectionName, string key)
        {
            FileInfo fileInfo = GetStorageFilePath(collectionName, key);

            if (fileInfo.Exists) fileInfo.Delete();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Reads a file representing a grain state object.
        /// </summary>
        /// <param name="collectionName">The type of the grain state object.</param>
        /// <param name="key">The grain id string.</param>
        /// <returns>Completion promise for this operation.</returns>
        public async Task<string> Read(string collectionName, string key)
        {
            FileInfo fileInfo = GetStorageFilePath(collectionName, key);

            if (!fileInfo.Exists)
                return null;

            using (var stream = fileInfo.OpenText())
            {
                return await stream.ReadToEndAsync();
            }
        }

        /// <summary>
        /// Writes a file representing a grain state object.
        /// </summary>
        /// <param name="collectionName">The type of the grain state object.</param>
        /// <param name="key">The grain id string.</param>
        /// <param name="entityData">The grain state data to be stored./</param>
        /// <returns>Completion promise for this operation.</returns>
        public async Task Write(string collectionName, string key, string entityData)
        {
            FileInfo fileInfo = GetStorageFilePath(collectionName, key);

            using (var stream = new StreamWriter(fileInfo.Open(FileMode.Create, FileAccess.Write)))
            {
                await stream.WriteAsync(entityData);
            }
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Returns the file path for storing that data with these keys.
        /// </summary>
        /// <param name="collectionName">The type of the grain state object.</param>
        /// <param name="key">The grain id string.</param>
        /// <returns>File info for this storage data file.</returns>
        private FileInfo GetStorageFilePath(string collectionName, string key)
        {
            string fileName = key + "." + collectionName;
            string path = Path.Combine(directory.FullName, fileName);
            return new FileInfo(path);
        }

    }
}
