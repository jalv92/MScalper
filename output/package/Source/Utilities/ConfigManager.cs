// Compatible with NinjaTrader 8.1.4.1
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MScalper.Utilities
{
    /// <summary>
    /// Manages configuration settings for the trading system
    /// </summary>
    public class ConfigManager
    {
        #region Private Fields
        private string _configDirectory;
        private string _mainConfigFile;
        private Dictionary<string, JObject> _configCache;
        private readonly object _lockObject = new object();
        private bool _autoSave;
        private DateTime _lastConfigCheck;
        private readonly TimeSpan _configCheckInterval = TimeSpan.FromSeconds(10);
        private static ConfigManager _instance;
        private bool _initialized;
        #endregion

        #region Events
        /// <summary>
        /// Event raised when configuration is updated
        /// </summary>
        public event EventHandler<ConfigUpdatedEventArgs> ConfigUpdated;
        
        /// <summary>
        /// Event arguments for configuration updates
        /// </summary>
        public class ConfigUpdatedEventArgs : EventArgs
        {
            /// <summary>
            /// Name of the configuration section that was updated
            /// </summary>
            public string SectionName { get; set; }
            
            /// <summary>
            /// Whether this was a new section or update to existing one
            /// </summary>
            public bool IsNewSection { get; set; }
        }
        #endregion

        #region Singleton Instance
        /// <summary>
        /// Gets the singleton instance of the ConfigManager
        /// </summary>
        public static ConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ConfigManager();
                }
                return _instance;
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Private constructor for singleton pattern
        /// </summary>
        private ConfigManager()
        {
            _configCache = new Dictionary<string, JObject>();
            _autoSave = true;
            _lastConfigCheck = DateTime.MinValue;
            _initialized = false;
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes the configuration manager
        /// </summary>
        /// <param name="configDirectory">Directory containing configuration files</param>
        /// <param name="mainConfigFile">Main configuration file name</param>
        /// <param name="autoSave">Whether to automatically save changes</param>
        /// <returns>True if initialization was successful</returns>
        public bool Initialize(string configDirectory, string mainConfigFile = "strategy_params.json", bool autoSave = true)
        {
            if (_initialized)
                return true;
                
            try
            {
                // Ensure directory exists
                if (!Directory.Exists(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                }
                
                _configDirectory = configDirectory;
                _mainConfigFile = mainConfigFile;
                _autoSave = autoSave;
                
                // Initial load of main config file
                string mainConfigPath = Path.Combine(_configDirectory, _mainConfigFile);
                if (File.Exists(mainConfigPath))
                {
                    LoadConfigFile(_mainConfigFile);
                }
                else
                {
                    // Create default config
                    var defaultConfig = new JObject();
                    defaultConfig["Version"] = "1.0.0";
                    defaultConfig["LastUpdated"] = DateTime.Now.ToString("o");
                    defaultConfig["Settings"] = new JObject();
                    
                    _configCache[_mainConfigFile] = defaultConfig;
                    
                    // Save default config if auto-save is enabled
                    if (_autoSave)
                    {
                        SaveConfigFile(_mainConfigFile);
                    }
                }
                
                // Look for other config files in the directory
                foreach (string filePath in Directory.GetFiles(_configDirectory, "*.json"))
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName != _mainConfigFile && !_configCache.ContainsKey(fileName))
                    {
                        LoadConfigFile(fileName);
                    }
                }
                
                _initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error initializing ConfigManager: {ex.Message}", Logger.LogLevel.Error);
                return false;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Gets a configuration value by section and key
        /// </summary>
        /// <typeparam name="T">Type to convert the value to</typeparam>
        /// <param name="section">Configuration section</param>
        /// <param name="key">Configuration key</param>
        /// <param name="defaultValue">Default value if not found</param>
        /// <returns>Configuration value or default</returns>
        public T GetValue<T>(string section, string key, T defaultValue = default)
        {
            if (!_initialized)
                throw new InvalidOperationException("ConfigManager not initialized");
                
            // Check if we should reload config from disk
            CheckForConfigChanges();
            
            lock (_lockObject)
            {
                try
                {
                    if (_configCache.ContainsKey(_mainConfigFile))
                    {
                        var config = _configCache[_mainConfigFile];
                        
                        if (config[section] != null && config[section][key] != null)
                        {
                            return config[section][key].ToObject<T>();
                        }
                    }
                    
                    // Try to find in other config files
                    foreach (var configFile in _configCache.Keys)
                    {
                        if (configFile == _mainConfigFile)
                            continue;
                            
                        var config = _configCache[configFile];
                        
                        if (config[section] != null && config[section][key] != null)
                        {
                            return config[section][key].ToObject<T>();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error getting config value {section}.{key}: {ex.Message}", Logger.LogLevel.Error);
                }
                
                return defaultValue;
            }
        }
        
        /// <summary>
        /// Sets a configuration value
        /// </summary>
        /// <typeparam name="T">Type of value</typeparam>
        /// <param name="section">Configuration section</param>
        /// <param name="key">Configuration key</param>
        /// <param name="value">Value to set</param>
        /// <param name="configFile">Optional specific config file (defaults to main)</param>
        /// <returns>True if successfully set</returns>
        public bool SetValue<T>(string section, string key, T value, string configFile = null)
        {
            if (!_initialized)
                throw new InvalidOperationException("ConfigManager not initialized");
                
            lock (_lockObject)
            {
                try
                {
                    string targetFile = configFile ?? _mainConfigFile;
                    
                    // Create config cache entry if it doesn't exist
                    if (!_configCache.ContainsKey(targetFile))
                    {
                        _configCache[targetFile] = new JObject();
                    }
                    
                    var config = _configCache[targetFile];
                    
                    // Create section if it doesn't exist
                    if (config[section] == null)
                    {
                        config[section] = new JObject();
                    }
                    
                    bool isNewValue = config[section][key] == null;
                    
                    // Set the value
                    config[section][key] = JToken.FromObject(value);
                    
                    // Update timestamp
                    config["LastUpdated"] = DateTime.Now.ToString("o");
                    
                    // Save if auto-save is enabled
                    if (_autoSave)
                    {
                        SaveConfigFile(targetFile);
                    }
                    
                    // Raise event
                    OnConfigUpdated(section, isNewValue);
                    
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error setting config value {section}.{key}: {ex.Message}", Logger.LogLevel.Error);
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Gets all values in a configuration section
        /// </summary>
        /// <param name="section">Section name</param>
        /// <returns>Dictionary of keys and values or null if section not found</returns>
        public Dictionary<string, object> GetSection(string section)
        {
            if (!_initialized)
                throw new InvalidOperationException("ConfigManager not initialized");
                
            // Check if we should reload config from disk
            CheckForConfigChanges();
            
            lock (_lockObject)
            {
                try
                {
                    // First check main config file
                    if (_configCache.ContainsKey(_mainConfigFile))
                    {
                        var config = _configCache[_mainConfigFile];
                        
                        if (config[section] != null)
                        {
                            var sectionObj = config[section] as JObject;
                            if (sectionObj != null)
                            {
                                return sectionObj.ToObject<Dictionary<string, object>>();
                            }
                        }
                    }
                    
                    // Then check other config files
                    foreach (var configFile in _configCache.Keys)
                    {
                        if (configFile == _mainConfigFile)
                            continue;
                            
                        var config = _configCache[configFile];
                        
                        if (config[section] != null)
                        {
                            var sectionObj = config[section] as JObject;
                            if (sectionObj != null)
                            {
                                return sectionObj.ToObject<Dictionary<string, object>>();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error getting config section {section}: {ex.Message}", Logger.LogLevel.Error);
                }
                
                return null;
            }
        }
        
        /// <summary>
        /// Sets an entire configuration section
        /// </summary>
        /// <param name="section">Section name</param>
        /// <param name="values">Values to set</param>
        /// <param name="configFile">Optional specific config file (defaults to main)</param>
        /// <returns>True if successfully set</returns>
        public bool SetSection(string section, Dictionary<string, object> values, string configFile = null)
        {
            if (!_initialized)
                throw new InvalidOperationException("ConfigManager not initialized");
                
            lock (_lockObject)
            {
                try
                {
                    string targetFile = configFile ?? _mainConfigFile;
                    
                    // Create config cache entry if it doesn't exist
                    if (!_configCache.ContainsKey(targetFile))
                    {
                        _configCache[targetFile] = new JObject();
                    }
                    
                    var config = _configCache[targetFile];
                    
                    bool isNewSection = config[section] == null;
                    
                    // Convert values to JObject
                    var sectionObj = JObject.FromObject(values);
                    
                    // Set the section
                    config[section] = sectionObj;
                    
                    // Update timestamp
                    config["LastUpdated"] = DateTime.Now.ToString("o");
                    
                    // Save if auto-save is enabled
                    if (_autoSave)
                    {
                        SaveConfigFile(targetFile);
                    }
                    
                    // Raise event
                    OnConfigUpdated(section, isNewSection);
                    
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error setting config section {section}: {ex.Message}", Logger.LogLevel.Error);
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Gets a config file object
        /// </summary>
        /// <param name="fileName">File name (null for main config)</param>
        /// <returns>Config object or null if not found</returns>
        public JObject GetConfigObject(string fileName = null)
        {
            if (!_initialized)
                throw new InvalidOperationException("ConfigManager not initialized");
                
            // Check if we should reload config from disk
            CheckForConfigChanges();
            
            lock (_lockObject)
            {
                string targetFile = fileName ?? _mainConfigFile;
                
                if (_configCache.ContainsKey(targetFile))
                {
                    return _configCache[targetFile];
                }
                
                return null;
            }
        }
        
        /// <summary>
        /// Saves all modified configurations to disk
        /// </summary>
        /// <returns>True if all saves were successful</returns>
        public bool SaveAllConfigs()
        {
            if (!_initialized)
                throw new InvalidOperationException("ConfigManager not initialized");
                
            bool allSuccess = true;
            
            lock (_lockObject)
            {
                foreach (string configFile in _configCache.Keys)
                {
                    if (!SaveConfigFile(configFile))
                        allSuccess = false;
                }
            }
            
            return allSuccess;
        }
        
        /// <summary>
        /// Reloads configurations from disk
        /// </summary>
        /// <returns>True if all reloads were successful</returns>
        public bool ReloadAllConfigs()
        {
            if (!_initialized)
                throw new InvalidOperationException("ConfigManager not initialized");
                
            bool allSuccess = true;
            
            lock (_lockObject)
            {
                List<string> configFiles = _configCache.Keys.ToList();
                
                foreach (string configFile in configFiles)
                {
                    if (!LoadConfigFile(configFile))
                        allSuccess = false;
                }
            }
            
            return allSuccess;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Loads a configuration file into the cache
        /// </summary>
        /// <param name="fileName">File name to load</param>
        /// <returns>True if successful</returns>
        private bool LoadConfigFile(string fileName)
        {
            try
            {
                string filePath = Path.Combine(_configDirectory, fileName);
                
                if (!File.Exists(filePath))
                    return false;
                    
                string json = File.ReadAllText(filePath);
                var config = JObject.Parse(json);
                
                lock (_lockObject)
                {
                    _configCache[fileName] = config;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error loading config file {fileName}: {ex.Message}", Logger.LogLevel.Error);
                return false;
            }
        }
        
        /// <summary>
        /// Saves a configuration file from the cache
        /// </summary>
        /// <param name="fileName">File name to save</param>
        /// <returns>True if successful</returns>
        private bool SaveConfigFile(string fileName)
        {
            try
            {
                lock (_lockObject)
                {
                    if (!_configCache.ContainsKey(fileName))
                        return false;
                        
                    string filePath = Path.Combine(_configDirectory, fileName);
                    string json = JsonConvert.SerializeObject(_configCache[fileName], Formatting.Indented);
                    
                    File.WriteAllText(filePath, json);
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error saving config file {fileName}: {ex.Message}", Logger.LogLevel.Error);
                return false;
            }
        }
        
        /// <summary>
        /// Checks if config files have been modified on disk
        /// </summary>
        private void CheckForConfigChanges()
        {
            // Only check periodically
            if ((DateTime.Now - _lastConfigCheck) < _configCheckInterval)
                return;
                
            _lastConfigCheck = DateTime.Now;
            
            try
            {
                lock (_lockObject)
                {
                    foreach (string fileName in _configCache.Keys)
                    {
                        string filePath = Path.Combine(_configDirectory, fileName);
                        
                        if (File.Exists(filePath))
                        {
                            DateTime lastWrite = File.GetLastWriteTime(filePath);
                            
                            // Check if config has LastUpdated field
                            if (_configCache[fileName]["LastUpdated"] != null)
                            {
                                DateTime lastUpdated = DateTime.Parse(_configCache[fileName]["LastUpdated"].ToString());
                                
                                // If file on disk is newer, reload it
                                if (lastWrite > lastUpdated)
                                {
                                    LoadConfigFile(fileName);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error checking for config changes: {ex.Message}", Logger.LogLevel.Error);
            }
        }
        
        /// <summary>
        /// Raises the ConfigUpdated event
        /// </summary>
        /// <param name="sectionName">Name of the updated section</param>
        /// <param name="isNewSection">Whether it's a new section</param>
        private void OnConfigUpdated(string sectionName, bool isNewSection)
        {
            ConfigUpdated?.Invoke(this, new ConfigUpdatedEventArgs
            {
                SectionName = sectionName,
                IsNewSection = isNewSection
            });
        }
        #endregion
    }
}
