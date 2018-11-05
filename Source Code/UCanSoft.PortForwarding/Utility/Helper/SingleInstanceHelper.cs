namespace UCanSoft.PortForwarding.Utility.Helper
{
    #region 单实例辅助类
    /// <summary>
    /// 单实例辅助类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SingleInstanceHelper<T>
        where T : SingleInstanceHelper<T>, new()
    {

        #region Field Define
        #endregion

        #region .Ctor
        /// <summary>
        /// .Ctor
        /// </summary>
        static SingleInstanceHelper()
        {
            Instance = new T();
            Instance.Init();
        }
        #endregion

        #region Property Define
        /// <summary>
        /// 单实例
        /// </summary>
        public static T Instance { get; private set; } = default(T);
        #endregion

        /// <summary>
        /// 初始化
        /// </summary>
        protected virtual void Init() { }
    }
    #endregion
}
