﻿/*
Technitium Library
Copyright (C) 2020  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TechnitiumLibrary.IO;

namespace TechnitiumLibrary.Net.Dns.ResourceRecords
{
    public class DnsTXTRecord : DnsResourceRecordData
    {
        string _text;

        public DnsTXTRecord(dynamic jsonResourceRecord)
        {
            _length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);

            _text = DnsDatagram.DecodeCharacterString(jsonResourceRecord.data.Value);
        }

        #region protected

        protected virtual void Parse(Stream s)
        {
            int bytesRead = 0;
            int length;

            while (bytesRead < _length)
            {
                length = s.ReadByte();
                if (length < 0)
                    throw new EndOfStreamException();

                if (_text == null)
                    _text = Encoding.ASCII.GetString(s.ReadBytes(length));
                else
                    _text += Encoding.ASCII.GetString(s.ReadBytes(length));

                bytesRead += length + 1;
            }
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries)
        {
            byte[] data = Encoding.ASCII.GetBytes(_text);
            int offset = 0;
            int length;

            do
            {
                length = data.Length - offset;
                if (length > 255)
                    length = 255;

                s.WriteByte(Convert.ToByte(length));
                s.Write(data, offset, length);

                offset += length;
            }
            while (offset < data.Length);
        }

        #endregion

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            DnsTXTRecord other = obj as DnsTXTRecord;
            if (other == null)
                return false;

            return this._text.Equals(other._text);
        }

        public override int GetHashCode()
        {
            return _text.GetHashCode();
        }

        public override string ToString()
        {
            return DnsDatagram.EncodeCharacterString(_text);
        }
    }
}
