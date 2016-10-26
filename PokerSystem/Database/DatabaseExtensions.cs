using NHibernate;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace CoinPokerServer.PokerSystem.Database
{
    public abstract class ImmutableUserType : IUserType
    {
        public new virtual bool Equals(object x, object y)
        {
            return object.Equals(x, y);
        }

        public virtual int GetHashCode(object x)
        {
            return (x == null) ? 0 : x.GetHashCode();
        }

        public bool IsMutable
        {
            get { return false; }
        }

        public object DeepCopy(object value)
        {
            return value;
        }

        public object Replace(object original, object target, object owner)
        {
            return original;
        }

        public object Assemble(object cached, object owner)
        {
            return cached;
        }

        public object Disassemble(object value)
        {
            return value;
        }

        public abstract object NullSafeGet(System.Data.IDataReader rs, string[] names, object owner);

        public abstract void NullSafeSet(System.Data.IDbCommand cmd, object value, int index);
        public abstract Type ReturnedType { get; }

        public abstract SqlType[] SqlTypes { get; }
    }

    public class TimeSpanUserType : ImmutableUserType
    {
        public override object NullSafeGet(IDataReader rs, string[] names, object owner)
        {
            // Need to do some formatting of TimeSpanTest before it can be parsed
            return TimeSpan.Parse((string)rs[names[0]]);
        }

        public override void NullSafeSet(IDbCommand cmd, object value, int index)
        {
            var timespan = (TimeSpan)value;
            var duration = timespan.Duration();
            var timeSpanstring = string.Format("{0}{1} {2}:{3}:{4}.{5}",
                (timespan.Ticks < 0) ? "-" : "+",
                duration.Days.ToString().PadLeft(2, '0'),
                duration.Hours.ToString().PadLeft(2, '0'),
                duration.Minutes.ToString().PadLeft(2, '0'),
                duration.Seconds.ToString().PadLeft(2, '0'),
                duration.Milliseconds.ToString().PadLeft(6, '0'));

            NHibernateUtil.String.NullSafeSet(cmd, timeSpanstring, index);
        }

        public override Type ReturnedType
        {
            get { return typeof(TimeSpan); }
        }

        public override SqlType[] SqlTypes
        {
            get { return new[] { SqlTypeFactory.GetString(8) }; }
        }
    }

}
