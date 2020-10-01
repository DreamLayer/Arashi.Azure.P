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
    public class DnsMXRecord : DnsResourceRecordData
    {
        ushort _preference;
        string _exchange;

        public DnsMXRecord(dynamic jsonResourceRecord)
        {
            _length = Convert.ToUInt16(jsonResourceRecord.data.Value.Length);
            string[] parts = (jsonResourceRecord.data.Value as string).Split(' ');
            _preference = ushort.Parse(parts[0]);
            _exchange = parts[1].TrimEnd('.');
        }

        protected override void WriteRecordData(Stream s, List<DnsDomainOffset> domainEntries)
        {
            DnsDatagram.WriteUInt16NetworkOrder(_preference, s);
            DnsDatagram.SerializeDomainName(_exchange, s, domainEntries);
        }
    }
}