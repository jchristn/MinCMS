namespace MinCms.Server
{
    using SwiftStack.Serialization;
    using CoreSerializer = MinCms.Core.Serialization.Serializer;

    /// <summary>
    /// MinCMS serializer implementation that implements SwiftStack's ISerializer interface
    /// and uses MinCms.Core.Serialization converters.
    /// </summary>
    public class MinCmsSerializer : ISerializer
    {
        #region Private-Members

        private readonly CoreSerializer _Serializer;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public MinCmsSerializer()
        {
            _Serializer = new CoreSerializer();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Deserialize JSON to an object instance.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="json">JSON.</param>
        /// <returns>Object instance.</returns>
        public T DeserializeJson<T>(string json)
        {
            return _Serializer.DeserializeJson<T>(json);
        }

        /// <summary>
        /// Deserialize bytes containing JSON to an object instance.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="bytes">Bytes containing JSON.</param>
        /// <returns>Object instance.</returns>
        public T DeserializeJson<T>(byte[] bytes)
        {
            return _Serializer.DeserializeJson<T>(bytes);
        }

        /// <summary>
        /// Serialize object instance to JSON.
        /// </summary>
        /// <param name="obj">Object instance.</param>
        /// <param name="pretty">True to enable pretty-print.</param>
        /// <returns>JSON string.</returns>
        public string SerializeJson(object obj, bool pretty = false)
        {
            return _Serializer.SerializeJson(obj, pretty);
        }

        #endregion
    }
}
