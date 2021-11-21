using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.Models
{
    /// <summary>
    /// Информация о процессе Linux.
    /// </summary>
    public class mPidInfo :IEquatable<mPidInfo>
    {
        /// <summary>
        /// Linux process Id.
        /// </summary>
        public UInt32 pid;

        /// <summary>
        /// Информация которую показывает systemctl status.
        /// </summary>
        public string sysctlInfo;

        /// <summary>
        /// Идентификатор браузера.
        /// </summary>
        public int browserId=-1;

        /// <summary>
        /// Сравнение для возможности использовать Linq.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(mPidInfo other)
        {
            if (other is null)
                return false;

            return this.pid == other.pid;
        }

        public override bool Equals(object obj) => Equals(obj as mPidInfo);
        public override int GetHashCode() => (pid).GetHashCode();
    }
}
