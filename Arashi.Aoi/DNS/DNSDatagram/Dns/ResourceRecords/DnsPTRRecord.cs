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

namespace TechnitiumLibrary.Net.Dns.ResourceRecords
{
    public class DnsPTRRecord : DnsResourceRecordData
    {
        #region variables

        string _domain;

        #endregion

        #region constructor

        public DnsPTRRecord(Stream s)
            : base(s)
        { }

        public DnsPTRRecord(dynamic jsonResourceRecord)
        {
            _length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);

            _domain = (jsonResourceRecord.data.Value as string).TrimEnd('.');
        }

        #endregion

        #region protected

        protected override void Parse(Stream s)
        {
            _domain = DnsDatagram.DeserializeDomainName(s);
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries)
        {
            DnsDatagram.SerializeDomainName(_domain, s, domainEntries);
        }

        #endregion

        #region public

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            DnsPTRRecord other = obj as DnsPTRRecord;
            if (other == null)
                return false;

            return this._domain.Equals(other._domain, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return _domain.GetHashCode();
        }

        public override string ToString()
        {
            return _domain + ".";
        }

        #endregion

        #region properties

        public string Domain
        { get { return _domain; } }

        #endregion
    }
}
