namespace MinCms.Core.Serialization
{
#pragma warning disable CS8765
#pragma warning disable CS8604
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603

    using System;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Serializer.
    /// </summary>
    public class Serializer
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private ExceptionConverter<Exception> _ExceptionConverter = new ExceptionConverter<Exception>();
        private NameValueCollectionConverter _NameValueCollectionConverter = new NameValueCollectionConverter();
        private DateTimeConverter _DateTimeConverter = new DateTimeConverter();
        private IPAddressConverter _IPAddressConverter = new IPAddressConverter();
        private StrictEnumConverterFactory _StrictEnumConverter = new StrictEnumConverterFactory();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Serializer()
        {
            InstantiateConverters();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Instantiation method to support fixups for various environments.
        /// </summary>
        public void InstantiateConverters()
        {
            try
            {
                Activator.CreateInstance<ExceptionConverter<Exception>>();
                Activator.CreateInstance<NameValueCollectionConverter>();
                Activator.CreateInstance<DateTimeConverter>();
                Activator.CreateInstance<IPAddressConverter>();
                Activator.CreateInstance<StrictEnumConverterFactory>();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Deserialize JSON to an instance.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Instance.</returns>
        public T DeserializeJson<T>(string json)
        {
            if (String.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.AllowTrailingCommas = true;
            options.ReadCommentHandling = JsonCommentHandling.Skip;
            options.NumberHandling = JsonNumberHandling.AllowReadingFromString;

            options.Converters.Add(_ExceptionConverter);
            options.Converters.Add(_NameValueCollectionConverter);
            options.Converters.Add(_DateTimeConverter);
            options.Converters.Add(_IPAddressConverter);
            options.Converters.Add(_StrictEnumConverter);

            return JsonSerializer.Deserialize<T>(json, options);
        }

        /// <summary>
        /// Deserialize JSON to an instance.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="bytes">Bytes containing JSON.</param>
        /// <returns>Instance.</returns>
        public T DeserializeJson<T>(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 1) throw new ArgumentNullException(nameof(bytes));
            return DeserializeJson<T>(Encoding.UTF8.GetString(bytes));
        }

        /// <summary>
        /// Serialize object to JSON.
        /// </summary>
        /// <param name="obj">Object.</param>
        /// <param name="pretty">Pretty print.</param>
        /// <returns>JSON.</returns>
        public string SerializeJson(object obj, bool pretty = true)
        {
            if (obj == null) return null;

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

            options.Converters.Add(_ExceptionConverter);
            options.Converters.Add(_NameValueCollectionConverter);
            options.Converters.Add(_DateTimeConverter);
            options.Converters.Add(_IPAddressConverter);
            options.Converters.Add(_StrictEnumConverter);

            if (!pretty)
            {
                options.WriteIndented = false;
                return JsonSerializer.Serialize(obj, options);
            }
            else
            {
                options.WriteIndented = true;
                return JsonSerializer.Serialize(obj, options);
            }
        }

        /// <summary>
        /// Copy an object.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="o">Object.</param>
        /// <returns>Instance.</returns>
        public T CopyObject<T>(object o)
        {
            if (o == null) return default(T);
            string json = SerializeJson(o, false);
            T ret = DeserializeJson<T>(json);
            return ret;
        }

        #endregion

        #region Private-Methods

        #endregion
    }

#pragma warning restore CS8603
#pragma warning restore CS8602
#pragma warning restore CS8600
#pragma warning restore CS8604
#pragma warning restore CS8765
}
