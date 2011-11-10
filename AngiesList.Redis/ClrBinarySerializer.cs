using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace AngiesList.Redis
{
    public class ClrBinarySerializer : IValueSerializer
    {
        private const ushort RawDataFlag = 0xfa52;
        private static readonly ArraySegment<byte> NullArray = new ArraySegment<byte>(new byte[0]);

        public virtual byte[] Serialize(object value)
        {
            var item = SerializeImpl(value);

            var retVal = new byte[item.Data.Count + 2];
            Array.Copy(BitConverter.GetBytes(item.Flags), 0, retVal, 0, 2);
            Array.Copy(item.Data.Array, item.Data.Offset, retVal, 2, item.Data.Count);
            return retVal;
        }

        private CacheItem SerializeImpl(object value)
        {
            // raw data is a special case when some1 passes in a buffer (byte[] or ArraySegment<byte>)
            if (value is ArraySegment<byte>)
            {
                // ArraySegment<byte> is only passed in when a part of buffer is being
                // serialized, usually from a MemoryStream (To avoid duplicating arrays
                // the byte[] returned by MemoryStream.GetBuffer is placed into an ArraySegment.)
                return new CacheItem(RawDataFlag, (ArraySegment<byte>)value);
            }

            var tmpByteArray = value as byte[];

            // - or we just received a byte[]. No further processing is needed.
            if (tmpByteArray != null)
            {
                return new CacheItem(RawDataFlag, new ArraySegment<byte>(tmpByteArray));
            }

            ArraySegment<byte> data;
            TypeCode code = value == null ? TypeCode.DBNull : Type.GetTypeCode(value.GetType());

            switch (code)
            {
                case TypeCode.DBNull: data = this.SerializeNull(); break;
                case TypeCode.String: data = this.SerializeString((String)value); break;
                case TypeCode.Boolean: data = this.SerializeBoolean((Boolean)value); break;
                case TypeCode.Int16: data = this.SerializeInt16((Int16)value); break;
                case TypeCode.Int32: data = this.SerializeInt32((Int32)value); break;
                case TypeCode.Int64: data = this.SerializeInt64((Int64)value); break;
                case TypeCode.UInt16: data = this.SerializeUInt16((UInt16)value); break;
                case TypeCode.UInt32: data = this.SerializeUInt32((UInt32)value); break;
                case TypeCode.UInt64: data = this.SerializeUInt64((UInt64)value); break;
                case TypeCode.Char: data = this.SerializeChar((Char)value); break;
                case TypeCode.DateTime: data = this.SerializeDateTime((DateTime)value); break;
                case TypeCode.Double: data = this.SerializeDouble((Double)value); break;
                case TypeCode.Single: data = this.SerializeSingle((Single)value); break;
                default: data = this.SerializeObject(value); break;
            }

            return new CacheItem((ushort)((ushort)code | 0x0100), data);
        }

        public virtual object Deserialize(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }
            var objData = new byte[bytes.Length - 2];
            Array.Copy(bytes, 2, objData, 0, objData.Length);

            ushort typeFlags = BitConverter.ToUInt16(bytes, 0);

            var cacheItem = new CacheItem(typeFlags, new ArraySegment<byte>(objData));

            return Deserialize(cacheItem);
        }

        private object Deserialize(CacheItem item)
        {
            if (item.Data.Array == null)
                return null;

            if (item.Flags == RawDataFlag)
            {
                var tmp = item.Data;

                if (tmp.Count == tmp.Array.Length)
                    return tmp.Array;

                // we should never arrive here, but it's better to be safe than sorry
                var retval = new byte[tmp.Count];

                Array.Copy(tmp.Array, tmp.Offset, retval, 0, tmp.Count);

                return retval;
            }

            var code = (TypeCode)(item.Flags & 0x00ff);

            var data = item.Data;

            switch (code)
            {
                // incrementing a non-existing key then getting it
                // returns as a string, but the flag will be 0
                // so treat all 0 flagged items as string
                // this may help inter-client data management as well
                //
                // however we store 'null' as Empty + an empty array,
                // so this must special-cased for compatibilty with
                // earlier versions. we introduced DBNull as null marker in emc2.6
                case TypeCode.Empty:
                    return (data.Array == null || data.Count == 0)
                    ? null
                    : DeserializeString(data);

                case TypeCode.DBNull: return null;
                case TypeCode.String: return this.DeserializeString(data);
                case TypeCode.Boolean: return this.DeserializeBoolean(data);
                case TypeCode.Int16: return this.DeserializeInt16(data);
                case TypeCode.Int32: return this.DeserializeInt32(data);
                case TypeCode.Int64: return this.DeserializeInt64(data);
                case TypeCode.UInt16: return this.DeserializeUInt16(data);
                case TypeCode.UInt32: return this.DeserializeUInt32(data);
                case TypeCode.UInt64: return this.DeserializeUInt64(data);
                case TypeCode.Char: return this.DeserializeChar(data);
                case TypeCode.DateTime: return this.DeserializeDateTime(data);
                case TypeCode.Double: return this.DeserializeDouble(data);
                case TypeCode.Single: return this.DeserializeSingle(data);
                case TypeCode.Object: return this.DeserializeObject(data);
                default: throw new InvalidOperationException("Unknown TypeCode was returned: " + code);
            }
        }

        #region [ Typed serialization ]

        protected virtual ArraySegment<byte> SerializeNull()
        {
            return NullArray;
        }

        protected virtual ArraySegment<byte> SerializeString(string value)
        {
            return new ArraySegment<byte>(Encoding.UTF8.GetBytes((string)value));
        }

        protected virtual ArraySegment<byte> SerializeBoolean(bool value)
        {
            return new ArraySegment<byte>(BitConverter.GetBytes(value));
        }

        protected virtual ArraySegment<byte> SerializeInt16(Int16 value)
        {
            return new ArraySegment<byte>(BitConverter.GetBytes(value));
        }

        protected virtual ArraySegment<byte> SerializeInt32(Int32 value)
        {
            return new ArraySegment<byte>(BitConverter.GetBytes(value));
        }

        protected virtual ArraySegment<byte> SerializeInt64(Int64 value)
        {
            return new ArraySegment<byte>(BitConverter.GetBytes(value));
        }

        protected virtual ArraySegment<byte> SerializeUInt16(UInt16 value)
        {
            return new ArraySegment<byte>(BitConverter.GetBytes(value));
        }

        protected virtual ArraySegment<byte> SerializeUInt32(UInt32 value)
        {
            return new ArraySegment<byte>(BitConverter.GetBytes(value));
        }

        protected virtual ArraySegment<byte> SerializeUInt64(UInt64 value)
        {
            return new ArraySegment<byte>(BitConverter.GetBytes(value));
        }

        protected virtual ArraySegment<byte> SerializeChar(char value)
        {
            return new ArraySegment<byte>(BitConverter.GetBytes(value));
        }

        protected virtual ArraySegment<byte> SerializeDateTime(DateTime value)
        {
            return new ArraySegment<byte>(BitConverter.GetBytes(value.ToBinary()));
        }

        protected virtual ArraySegment<byte> SerializeDouble(Double value)
        {
            return new ArraySegment<byte>(BitConverter.GetBytes(value));
        }

        protected virtual ArraySegment<byte> SerializeSingle(Single value)
        {
            return new ArraySegment<byte>(BitConverter.GetBytes(value));
        }

        protected virtual ArraySegment<byte> SerializeObject(object value)
        {
            using (var ms = new MemoryStream())
            {
                new BinaryFormatter().Serialize(ms, value);

                return new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        #endregion
        #region [ Typed deserialization ]

        protected virtual String DeserializeString(ArraySegment<byte> value)
        {
            return Encoding.UTF8.GetString(value.Array, value.Offset, value.Count);
        }

        protected virtual Boolean DeserializeBoolean(ArraySegment<byte> value)
        {
            return BitConverter.ToBoolean(value.Array, value.Offset);
        }

        protected virtual Int16 DeserializeInt16(ArraySegment<byte> value)
        {
            return BitConverter.ToInt16(value.Array, value.Offset);
        }

        protected virtual Int32 DeserializeInt32(ArraySegment<byte> value)
        {
            return BitConverter.ToInt32(value.Array, value.Offset);
        }

        protected virtual Int64 DeserializeInt64(ArraySegment<byte> value)
        {
            return BitConverter.ToInt64(value.Array, value.Offset);
        }

        protected virtual UInt16 DeserializeUInt16(ArraySegment<byte> value)
        {
            return BitConverter.ToUInt16(value.Array, value.Offset);
        }

        protected virtual UInt32 DeserializeUInt32(ArraySegment<byte> value)
        {
            return BitConverter.ToUInt32(value.Array, value.Offset);
        }

        protected virtual UInt64 DeserializeUInt64(ArraySegment<byte> value)
        {
            return BitConverter.ToUInt64(value.Array, value.Offset);
        }

        protected virtual Char DeserializeChar(ArraySegment<byte> value)
        {
            return BitConverter.ToChar(value.Array, value.Offset);
        }

        protected virtual DateTime DeserializeDateTime(ArraySegment<byte> value)
        {
            return DateTime.FromBinary(BitConverter.ToInt64(value.Array, value.Offset));
        }

        protected virtual Double DeserializeDouble(ArraySegment<byte> value)
        {
            return BitConverter.ToDouble(value.Array, value.Offset);
        }

        protected virtual Single DeserializeSingle(ArraySegment<byte> value)
        {
            return BitConverter.ToSingle(value.Array, value.Offset);
        }

        protected virtual object DeserializeObject(ArraySegment<byte> value)
        {
            using (var ms = new MemoryStream(value.Array, value.Offset, value.Count))
            {
                return new BinaryFormatter().Deserialize(ms);
            }
        }
        #endregion


        private struct CacheItem
        {
            private ArraySegment<byte> data;
            private ushort flags;

            /// <summary>
            /// Initializes a new instance of <see cref="T:CacheItem"/>.
            /// </summary>
            /// <param name="flags">Custom item data.</param>
            /// <param name="data">The serialized item.</param>
            public CacheItem(ushort flags, ArraySegment<byte> data)
            {
                this.data = data;
                this.flags = flags;
            }

            /// <summary>
            /// The data representing the item being stored/retireved.
            /// </summary>
            public ArraySegment<byte> Data
            {
                get { return this.data; }
                set { this.data = value; }
            }

            /// <summary>
            /// Flags set for this instance.
            /// </summary>
            public ushort Flags
            {
                get { return this.flags; }
                set { this.flags = value; }
            }
        }
    }

    #region Derivative works statement

    /*
        The above code is inspired by and derivative of the Apache License v2 code
        found at https://github.com/enyim/EnyimMemcached/blob/mb2.11/Enyim.Caching/Memcached/Transcoders/DefaultTranscoder.cs
        which at the time of forking was Copyrighted as stated below:
     */

    /* ************************************************************
     *
     * Copyright (c) 2010 Attila Kisk�, enyim.com
     *
     * Licensed under the Apache License, Version 2.0 (the "License");
     * you may not use this file except in compliance with the License.
     * You may obtain a copy of the License at
     *
     * http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     *
     * ************************************************************/
    #endregion
}
