namespace CDPIUI.Core.Store.Network
{
    /// <summary>
    /// Store internal proxy settings helper.
    /// </summary>
    public static class StoreProxyHelper
    {
        /// <summary>
        /// Use proxy for GitHub connections.
        /// Do not applies to other version controls.
        /// </summary>
        public static bool UseProxyForStoreConnections 
        {
            get
            {
                return
                    StoreHelper.Instance.VersionControl == Repository.SupportedVersionControls.GitHub &&
                    SettingsManager.Instance.GetValueOrDefault<bool>("STORE", "ProxyEnable", defaultValue: false);
            }
            set
            {
                SettingsManager.Instance.SetValue<bool>("STORE", "ProxyEnable", value);
            }
        }
    }
}
