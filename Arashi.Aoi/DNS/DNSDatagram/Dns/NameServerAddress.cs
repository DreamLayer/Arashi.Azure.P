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
using System.Net;
using System.Net.Sockets;
using System.Text;
using TechnitiumLibrary.IO;
using TechnitiumLibrary.Net.Dns.ResourceRecords;

namespace TechnitiumLibrary.Net.Dns
{
    public class NameServerAddress : IComparable<NameServerAddress>
    {
        #region variables

        DnsTransportProtocol _protocol;
        string _originalAddress;

        Uri _dohEndPoint;
        DomainEndPoint _domainEndPoint;
        IPEndPoint _ipEndPoint;

        bool _ipEndPointExpires;
        DateTime _ipEndPointExpiresOn;
        const int IP_ENDPOINT_DEFAULT_TTL = 900;

        #endregion

        #region constructors

        public NameServerAddress(NameServerAddress nameServer, DnsTransportProtocol protocol)
        {
            _protocol = protocol;
            _originalAddress = nameServer._originalAddress;

            _dohEndPoint = nameServer._dohEndPoint;
            _domainEndPoint = nameServer._domainEndPoint;
            _ipEndPoint = nameServer._ipEndPoint;

            _ipEndPointExpires = nameServer._ipEndPointExpires;
            _ipEndPointExpiresOn = nameServer._ipEndPointExpiresOn;

            ValidateProtocol();
        }

        public NameServerAddress(Uri dohEndPoint, DnsTransportProtocol protocol = DnsTransportProtocol.Https)
        {
            _dohEndPoint = dohEndPoint;

            if (IPAddress.TryParse(_dohEndPoint.Host, out IPAddress address))
                _ipEndPoint = new IPEndPoint(address, _dohEndPoint.Port);

            _protocol = protocol;
            _originalAddress = _dohEndPoint.AbsoluteUri;

            ValidateProtocol();
        }

        public NameServerAddress(Uri dohEndPoint, IPAddress address, DnsTransportProtocol protocol = DnsTransportProtocol.Https)
        {
            _dohEndPoint = dohEndPoint;
            _ipEndPoint = new IPEndPoint(address, _dohEndPoint.Port);

            _protocol = protocol;

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                _originalAddress = _dohEndPoint.AbsoluteUri + " ([" + address.ToString() + "])";
            else
                _originalAddress = _dohEndPoint.AbsoluteUri + " (" + address.ToString() + ")";

            ValidateProtocol();
        }

        public NameServerAddress(string address, DnsTransportProtocol protocol)
        {
            Parse(address.Trim());
            _protocol = protocol;
            ValidateProtocol();
        }

        public NameServerAddress(string address)
        {
            Parse(address.Trim());
            GuessProtocol();
        }

        public NameServerAddress(IPAddress address, DnsTransportProtocol protocol = DnsTransportProtocol.Udp)
        {
            _ipEndPoint = new IPEndPoint(address, 53);

            _protocol = protocol;
            _originalAddress = address.ToString();

            ValidateProtocol();
        }

        public NameServerAddress(string domain, IPAddress address, DnsTransportProtocol protocol = DnsTransportProtocol.Udp)
        {
            _domainEndPoint = new DomainEndPoint(domain, 53);
            _ipEndPoint = new IPEndPoint(address, 53);

            _protocol = protocol;

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                _originalAddress = domain + " ([" + address.ToString() + "])";
            else
                _originalAddress = domain + " (" + address.ToString() + ")";

            ValidateProtocol();
        }

        public NameServerAddress(string domain, IPEndPoint ipEndPoint, DnsTransportProtocol protocol = DnsTransportProtocol.Udp)
        {
            _domainEndPoint = new DomainEndPoint(domain, ipEndPoint.Port);
            _ipEndPoint = ipEndPoint;

            _protocol = protocol;

            if (ipEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
                _originalAddress = domain + " ([" + ipEndPoint.Address.ToString() + "]:" + ipEndPoint.Port + ")";
            else
                _originalAddress = domain + " (" + ipEndPoint.ToString() + ")";

            ValidateProtocol();
        }

        public NameServerAddress(EndPoint endPoint, DnsTransportProtocol protocol = DnsTransportProtocol.Udp)
        {
            switch (endPoint.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                case AddressFamily.InterNetworkV6:
                    _ipEndPoint = endPoint as IPEndPoint;
                    break;

                case AddressFamily.Unspecified:
                    _domainEndPoint = endPoint as DomainEndPoint;
                    break;

                default:
                    throw new NotSupportedException("AddressFamily not supported.");
            }

            _protocol = protocol;

            if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
                _originalAddress = "[" + (endPoint as IPEndPoint).Address.ToString() + "]:" + (endPoint as IPEndPoint).Port;
            else
                _originalAddress = endPoint.ToString();

            ValidateProtocol();
        }

        public NameServerAddress(BinaryReader bR)
        {
            switch (bR.ReadByte())
            {
                case 1:
                    if (bR.ReadBoolean())
                        _dohEndPoint = new Uri(bR.ReadShortString());

                    if (bR.ReadBoolean())
                        _domainEndPoint = EndPointExtension.Parse(bR) as DomainEndPoint;

                    if (bR.ReadBoolean())
                        _ipEndPoint = EndPointExtension.Parse(bR) as IPEndPoint;

                    if (_dohEndPoint != null)
                        _originalAddress = _dohEndPoint.AbsoluteUri;
                    else if (_ipEndPoint != null)
                        _originalAddress = _ipEndPoint.ToString();
                    else if (_domainEndPoint != null)
                        _originalAddress = _domainEndPoint.ToString();

                    GuessProtocol();
                    break;

                case 2:
                    Parse(bR.ReadShortString());
                    GuessProtocol();
                    break;

                case 3:
                    _protocol = (DnsTransportProtocol)bR.ReadByte();
                    Parse(bR.ReadShortString());
                    break;

                default:
                    throw new InvalidDataException("NameServerAddress version not supported");
            }
        }

        #endregion

        #region private

        private void ValidateProtocol()
        {
            switch (_protocol)
            {
                case DnsTransportProtocol.Udp:
                case DnsTransportProtocol.Tcp:
                    if (_dohEndPoint != null)
                        throw new ArgumentException("Invalid DNS transport protocol was specified for current operation: " + _protocol.ToString());

                    if (Port == 853)
                        throw new ArgumentException("Invalid DNS transport protocol was specified for current operation: " + _protocol.ToString());

                    break;

                case DnsTransportProtocol.Tls:
                    if (_dohEndPoint != null)
                        throw new ArgumentException("Invalid DNS transport protocol was specified for current operation: " + _protocol.ToString());

                    if (Port == 53)
                        throw new ArgumentException("Invalid DNS transport protocol was specified for current operation: " + _protocol.ToString());

                    break;

                case DnsTransportProtocol.Https:
                case DnsTransportProtocol.HttpsJson:
                    if (_dohEndPoint == null)
                        throw new ArgumentException("Invalid DNS transport protocol was specified for current operation: " + _protocol.ToString());

                    switch (Port)
                    {
                        case 53:
                        case 853:
                            throw new ArgumentException("Invalid DNS transport protocol was specified for current operation: " + _protocol.ToString());
                    }

                    break;
            }
        }

        private void GuessProtocol()
        {
            if (_dohEndPoint != null)
            {
                _protocol = DnsTransportProtocol.Https;
            }
            else
            {
                switch (Port)
                {
                    case 853:
                        _protocol = DnsTransportProtocol.Tls;
                        break;

                    default:
                        _protocol = DnsTransportProtocol.Udp;
                        break;
                }
            }
        }

        private void Parse(string address)
        {
            _originalAddress = address;

            //parse
            string domainName = null;
            int domainPort = 0;
            string host = null;
            int port = 0;
            bool ipv6Host = false;

            int posRoundBracketStart = address.IndexOf('(');
            if (posRoundBracketStart > -1)
            {
                int posRoundBracketEnd = address.IndexOf(')', posRoundBracketStart + 1);
                if (posRoundBracketEnd < 0)
                    throw new ArgumentException("Invalid name server address was encountered: " + _originalAddress);

                {
                    string strDomainPart = address.Substring(0, posRoundBracketStart).Trim();

                    if (strDomainPart.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || strDomainPart.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        _dohEndPoint = new Uri(strDomainPart);
                    }
                    else
                    {
                        string[] strParts = strDomainPart.Split(':');

                        domainName = strParts[0];

                        if (strParts.Length > 1)
                            domainPort = int.Parse(strParts[1]);
                    }
                }

                address = address.Substring(posRoundBracketStart + 1, posRoundBracketEnd - posRoundBracketStart - 1);
            }

            if (address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                _dohEndPoint = new Uri(address);
            }
            else if (address.StartsWith("["))
            {
                //ipv6
                if (address.EndsWith("]"))
                {
                    host = address.Trim('[', ']');
                }
                else
                {
                    int posBracketEnd = address.LastIndexOf(']');

                    host = address.Substring(1, posBracketEnd - 1);

                    int posCollon = address.IndexOf(':', posBracketEnd + 1);
                    if (posCollon > -1)
                        port = int.Parse(address.Substring(posCollon + 1));
                }

                ipv6Host = true;
            }
            else
            {
                string[] strParts = address.Split(':');

                host = strParts[0].Trim();

                if (strParts.Length > 1)
                    port = int.Parse(strParts[1]);
            }

            if (_dohEndPoint == null)
            {
                if ((domainPort == 0) && (port == 0))
                {
                    domainPort = 53;
                    port = 53;
                }
                else if (domainPort == 0)
                {
                    domainPort = port;
                }
                else if (port == 0)
                {
                    port = domainPort;
                }
                else if (domainPort != port)
                {
                    throw new ArgumentException("Invalid name server address was encountered: " + _originalAddress);
                }

                if (domainName != null)
                    _domainEndPoint = new DomainEndPoint(domainName, domainPort);

                if (IPAddress.TryParse(host, out IPAddress ipAddress))
                    _ipEndPoint = new IPEndPoint(ipAddress, port);
                else if ((_domainEndPoint != null) || ipv6Host)
                    throw new ArgumentException("Invalid name server address was encountered: " + _originalAddress);
                else
                    _domainEndPoint = new DomainEndPoint(host, port);
            }
            else if (host != null)
            {
                if (port == 0)
                    port = _dohEndPoint.Port;
                else if (_dohEndPoint.Port != port)
                    throw new ArgumentException("Invalid name server address was encountered: " + _originalAddress);

                if (IPAddress.TryParse(host, out IPAddress ipAddress))
                    _ipEndPoint = new IPEndPoint(ipAddress, port);
                else
                    throw new ArgumentException("Invalid name server address was encountered: " + _originalAddress);
            }
        }

        #endregion

        #region static

        public static List<NameServerAddress> GetNameServersFromResponse(DnsDatagram response, bool preferIPv6, bool selectOnlyNameServersWithGlue)
        {
            List<NameServerAddress> nameServers = new List<NameServerAddress>(response.Authority.Count);

            foreach (DnsResourceRecord authorityRecord in response.Authority)
            {
                if (authorityRecord.Type == DnsResourceRecordType.NS)
                {
                    DnsNSRecord nsRecord = (DnsNSRecord)authorityRecord.RDATA;
                    IPEndPoint endPoint = null;

                    //find ip address of authoritative name server from additional records
                    foreach (DnsResourceRecord rr in response.Additional)
                    {
                        if (nsRecord.NameServer.Equals(rr.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            switch (rr.Type)
                            {
                                case DnsResourceRecordType.A:
                                    endPoint = new IPEndPoint(((DnsARecord)rr.RDATA).Address, 53);
                                    nameServers.Add(new NameServerAddress(nsRecord.NameServer, endPoint));
                                    break;

                                case DnsResourceRecordType.AAAA:
                                    endPoint = new IPEndPoint(((DnsAAAARecord)rr.RDATA).Address, 53);

                                    if (preferIPv6)
                                        nameServers.Add(new NameServerAddress(nsRecord.NameServer, endPoint));

                                    break;
                            }
                        }
                    }

                    if ((endPoint == null) && !selectOnlyNameServersWithGlue)
                        nameServers.Add(new NameServerAddress(new DomainEndPoint(nsRecord.NameServer, 53)));
                }
            }

            return nameServers;
        }

        #endregion

        #region public

        public override string ToString()
        {
            string value;

            if (_dohEndPoint != null)
                value = _dohEndPoint.AbsoluteUri;
            else if (_domainEndPoint != null)
                value = _domainEndPoint.ToString();
            else
                return _ipEndPoint.ToString();

            if (_ipEndPoint != null)
                value += " (" + _ipEndPoint.ToString() + ")";

            return value;
        }

        public int CompareTo(NameServerAddress other)
        {
            if ((this._ipEndPoint == null) || (other._ipEndPoint == null))
                return 0;

            if ((this._ipEndPoint.AddressFamily == AddressFamily.InterNetwork) && (other._ipEndPoint.AddressFamily == AddressFamily.InterNetworkV6))
                return 1;

            if ((this._ipEndPoint.AddressFamily == AddressFamily.InterNetworkV6) && (other._ipEndPoint.AddressFamily == AddressFamily.InterNetwork))
                return -1;

            return 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            NameServerAddress other = obj as NameServerAddress;
            if (other == null)
                return false;

            if (!EqualityComparer<DnsTransportProtocol>.Default.Equals(_protocol, other._protocol))
                return false;

            if (!EqualityComparer<Uri>.Default.Equals(_dohEndPoint, other._dohEndPoint))
                return false;

            if (!EqualityComparer<DomainEndPoint>.Default.Equals(_domainEndPoint, other._domainEndPoint))
                return false;

            if (!EqualityComparer<IPEndPoint>.Default.Equals(_ipEndPoint, other._ipEndPoint))
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = 563096372;
            hashCode = hashCode * -1521134295 + EqualityComparer<DnsTransportProtocol>.Default.GetHashCode(_protocol);
            hashCode = hashCode * -1521134295 + EqualityComparer<Uri>.Default.GetHashCode(_dohEndPoint);
            hashCode = hashCode * -1521134295 + EqualityComparer<DomainEndPoint>.Default.GetHashCode(_domainEndPoint);
            hashCode = hashCode * -1521134295 + EqualityComparer<IPEndPoint>.Default.GetHashCode(_ipEndPoint);
            return hashCode;
        }

        #endregion

        #region properties

        public DnsTransportProtocol Protocol
        { get { return _protocol; } }

        public string OriginalAddress
        { get { return _originalAddress; } }

        public string Host
        {
            get
            {
                if (_dohEndPoint != null)
                    return _dohEndPoint.Host;

                if (_domainEndPoint != null)
                    return _domainEndPoint.Address;

                return _ipEndPoint.Address.ToString();
            }
        }

        public int Port
        {
            get
            {
                if (_dohEndPoint != null)
                    return _dohEndPoint.Port;

                if (_domainEndPoint != null)
                    return _domainEndPoint.Port;

                return _ipEndPoint.Port;
            }
        }

        public Uri DnsOverHttpEndPoint
        { get { return _dohEndPoint; } }

        public DomainEndPoint DomainEndPoint
        { get { return _domainEndPoint; } }

        public IPEndPoint IPEndPoint
        { get { return _ipEndPoint; } }

        public EndPoint EndPoint
        {
            get
            {
                if (_ipEndPoint != null)
                    return _ipEndPoint; //IP endpoint is prefered

                if (_dohEndPoint != null)
                    return new DomainEndPoint(_dohEndPoint.Host, _dohEndPoint.Port);

                return _domainEndPoint;
            }
        }

        public bool IsIPEndPointStale
        { get { return (_ipEndPoint == null) || (_ipEndPointExpires && (DateTime.UtcNow > _ipEndPointExpiresOn)); } }

        #endregion
    }
}
