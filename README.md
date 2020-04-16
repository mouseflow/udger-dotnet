# Udger client for .NET (data ver. 3)
Local parser is very fast and accurate useragent string detection solution. Enables developers to locally install and integrate a highly-scalable product.
We provide the detection of the devices (personal computer, tablet, Smart TV, Game console etc.), operating system, client SW type (browser, e-mail client etc.)
and devices market name (example: Sony Xperia Tablet S, Nokia Lumia 820 etc.).
It also provides information about IP addresses (Public proxies, VPN services, Tor exit nodes, Fake crawlers, Web scrapers, Datacenter name .. etc.)

### Requirements
- .NET Standard 2.1 or later.
- ADO.NET Data Provider for SQLite (included)
- datafile v3 (udgerdb_v3.dat) from https://data.udger.com/ 

### Automatic updates download
- for autoupdate data use Udger data updater (https://udger.com/support/documentation/?doc=62)

###Features
- Fast
- Thread safe
- Written in C#
- LRU cache
- Released under the MIT

### Usage
```csharp
using Udger.Parser;

UdgerParser parser = new UdgerParser();
// Set data dir (in this directory is stored data file: udgerdb_v3.dat)
// Data file can be downloaded manually from https://data.udger.com/, but we recommend use udger-updater (https://udger.com/support/documentation/?doc=62)
parser.SetDataDir(@"C:\udger");
// parse user agent and IP address
UserAgent ua = parser.ParseUserAgent("Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/48.0.2564.116 Safari/537.36");
IpAddress ip = parser.ParseIpAddress("2600:3c01::f03c:91ff:fe70:9208");
```

You can also change the capacity of the LRU cache (default 10000) or disable it.
```csharp
UdgerParser parser = new UdgerParser(useLRUCache: false);
UdgerParser parser = new UdgerParser(cacheCapacity: 1000);
```

### Documentation for programmers
- https://udger.com/pub/documentation/parser/NET/html/

### Author
The Udger.com Team (info@udger.com)

### Co-author
Mouseflow
developers@mouseflow.com
https://mouseflow.com
