namespace HCC2RestClient.Models;

public enum Quality
{
        /// <summary>
        /// The value is bad but no specific reason is known.
        /// </summary>
        Bad = 0,                        // BAD
        /// <summary>
        /// The value is good.
        /// </summary>
        Good = 192,                     // GOOD
        /// <summary>
        /// The value has been overridden. 
        /// </summary>
        Good_LocalOverride = 216,       // GOOD_LOCAL_OVERRIDE
        /// <summary>
        /// The value is good.
        /// </summary>
        Good_Extended = 131264,         // GOOD [Non-Specific] | 0x20000
        /// <summary>
        /// There is no specific reason why the value is uncertain or the producer 
        /// has determined the value is state.
        /// </summary>
        Stale = 64,                     // UNCERTIAN [Non-Specific] 
        /// <summary>
        /// Uncertain and Low Limit
        /// </summary>
        MinimumOutOfRange = 65,         // UNCERTIAN [Non-Specific] (Low Limited) 
        /// <summary>
        /// Uncertain and High Limit
        /// </summary>
        MaximumOutOfRange = 66,         // UNCERTIAN [Non-Specific] (High Limited) 
        /// <summary>
        /// Uncertain and constant.
        /// </summary>
        Frozen = 67,                    // UNCERTIAN [Non-Specific] (Constant) 
        /// <summary>
        /// The last usable value.
        /// </summary>
        InvalidFactorOffset = 68,       // UNCERTAIN_LAST_USABLE
        /// <summary>
        /// The block is off scan or otherwise locked. Value has not yet been updated.
        /// </summary>
        SetItemInactive = 28,           // BAD_OUT_OF_SERVICE
        /// <summary>
        /// Communications have failed. No last known value is available.
        /// </summary>
        CommunicationFailure = 24,      // BAD_COMM_FAILURE
        /// <summary>
        /// There is a server specific problem with the configuration.
        /// </summary>
        UnableToParse = 4,              // BAD_CONFIG_ERROR
        /// <summary>
        /// The input is required to be logically connected to something but the connection couldn't be established.
        /// </summary>
        DeviceNotConnected = 8,         // BAD_NOT_CONNECTED
        /// <summary>
        /// Empty data
        /// </summary>
        BadQualityNoData = 2097152

}