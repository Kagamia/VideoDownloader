<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Web.Extensions.dll</Reference>
  <Namespace>System.Collections.Concurrent</Namespace>
  <Namespace>System.Collections.Specialized</Namespace>
  <Namespace>System.Drawing</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Sockets</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Web.Script.Serialization</Namespace>
</Query>

/*
 * original： https://github.com/EvilCult/Video-Downloader/blob/master/Module/youkuClass.py
 */
void Main()
{
	var youku = new ChaseYouku();
	youku.videoLink = "s";
	youku.videoLink = "http://v.youku.com/v_show/id_XMTcwMDk3MjYzMg==.html";
	youku.ChaseUrl();
}

// Define other methods and classes here
class VideoInfo
{
	public VideoInfoData data;
}

class VideoInfoData
{
	public long id;
	public VideoInfoStream[] stream;
	public VideoInfoSecurity security;
	public VideoInfoDesc video;
}

class VideoInfoStream
{
	public string stream_type;
	public long size;
	public int width;
	public int height;
	public string stream_fileid;
	public VideoInfoStreamSegment[] segs;
}

class VideoInfoStreamSegment
{
	public string fileid;
	public string key;
	public int size;
}

class VideoInfoSecurity
{
	public string encrypt_string;
	public long ip;
}

class VideoInfoDesc
{
	public string title;
	public double seconds;
	public string encodeid;
}


public class HttpClient
{
	public CookieContainer Cookies = new CookieContainer();

	private string Serialize(NameValueCollection kv)
	{
		var sb = new StringBuilder();
		foreach (string key in kv.Keys)
		{
			if (sb.Length > 0) sb.Append("&");
			sb.Append(WebUtility.UrlEncode(key))
				.Append("=")
				.Append(WebUtility.UrlEncode(kv[key]));
		}
		return sb.ToString();
	}

	public HttpWebResponse Get(string url, NameValueCollection queryString)
	{
		return Get(url, queryString, null);
	}

	public HttpWebResponse Get(string url, NameValueCollection queryString, Action<HttpWebRequest> preLoad)
	{
		var req = CreateRequest(url, queryString);
		req.Method = "get";
		preLoad?.Invoke(req);
		return req.GetResponse() as HttpWebResponse;
	}

	public HttpWebResponse Post(string url, NameValueCollection queryString, NameValueCollection form)
	{
		var req = CreateRequest(url, queryString);
		req.Method = "post";
		if (form != null && form.Count > 0)
		{
			req.ContentType = "application/x-www-form-urlencoded";
			using (var input = req.GetRequestStream())
			{
				using (var sw = new StreamWriter(input, new UTF8Encoding(false)))
				{
					sw.Write(Serialize(form));
					sw.Flush();
				}
			}
		}
		return req.GetResponse() as HttpWebResponse;
	}

	private HttpWebRequest CreateRequest(string url, NameValueCollection queryString)
	{
		if (queryString != null && queryString.Count > 0)
		{
			url += "?" + Serialize(queryString);
		}
		var req = HttpWebRequest.CreateHttp(url);
		req.Accept = "*/*";
		req.Headers[HttpRequestHeader.AcceptCharset] = "utf-8";
		req.Headers[HttpRequestHeader.AcceptEncoding] = "gzip,deflate";
		req.UserAgent = "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.1 (KHTML, like Gecko) Chrome/21.0.1180.77 Safari/537.1";
		req.CookieContainer = this.Cookies;
		return req;
	}
}

public static class HttpExtensions
{
	public static string AsString(this HttpWebResponse resp)
	{
		if (resp.StatusCode != HttpStatusCode.OK) return null;
		var input = resp.GetResponseStream();
		if (string.Equals(resp.ContentEncoding, "gzip", StringComparison.InvariantCultureIgnoreCase))
		{
			input = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
		}
		else if (string.Equals(resp.ContentEncoding, "deflate", StringComparison.InvariantCultureIgnoreCase))
		{
			input = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress);
		}
		StreamReader sr = new StreamReader(input, new UTF8Encoding(false));
		string content = sr.ReadToEnd();
		input.Close();
		resp.Close();
		return content;
	}
}

class ChaseYouku {
	public string videoLink = "";
	public readonly string infoUrl = "http://play.youku.com/play/get.json?ct=12&vid=";
	public readonly string fileUrlPrefix = "http://pl.youku.com/playlist/m3u8?ctype=12&ev=1&keyframe=0";
	public readonly Dictionary<string, string> videoTypeList = new Dictionary<string, string>(){
		 {"n", "mp4hd"}, 
		 {"h", "mp4hd2"}, 
		 {"s", "mp4hd3"},
		 {"_0", "3gphd"},
		 {"_1", "flvhd"}
	};
	public readonly HttpClient client = new HttpClient();
	public string videoType = "s";
	public readonly long now = (long)(DateTime.Now-new DateTime(1970,1,1)).TotalMilliseconds;

	public void ChaseUrl() {
		var videoID = GetVideoID(videoLink);
		if (!string.IsNullOrEmpty(videoID)) {
			var info = GetVideoInfo(videoID);
			if (!string.IsNullOrEmpty(videoID)) {
				string fileUrl = GetVideoFileUrl(info);
				if (!string.IsNullOrEmpty(fileUrl)) {
					var listFile = GetFileList(fileUrl).Dump();
				} else {
					Console.WriteLine("无法获取m3u8地址，没有给定的清晰度参数");
				}
			} else {
				Console.WriteLine("连接错误");
			}
		} else {
			Console.WriteLine("url格式错误");
		}
	}
	
	private string GetVideoID(string link) {
		string videoID;
		var m = Regex.Match(link, @"id_(.*?==)");
		if (m.Success) {
			videoID = m.Result("$1");
		} else {
			m = Regex.Match(link, @"id_(.*?)\.html");
			if (m.Success) {
				videoID = m.Result("$1")+"==";
			} else {
				videoID = null;
			}
		}
		return videoID;
	}
	
	private string GetVideoInfo(string videoID) {
		try {
			var resp = client.Get(this.infoUrl + videoID, null, req=>{
				req.Referer = "http://c-h5.youku.com/";
			});
			var pageBody = resp.AsString();
			return pageBody;
		} catch {
			return null;
		}
	}
	
	private string GetVideoFileUrl(string videoInfoJson) {
		var videoInfo = new JavaScriptSerializer()
			.Deserialize<VideoInfo>(videoInfoJson);
		var ep = videoInfo.data.security.encrypt_string;
		string fileUrl = null;
		
		if (!string.IsNullOrEmpty(ep)) {
			var oip = videoInfo.data.security.ip;
			var vid = videoInfo.data.video.encodeid;
			var temp = Encoding.ASCII.GetString(RC4("becaf9be", Convert.FromBase64String(ep))).Split('_');
			var sid = temp[0];
			var token = temp[1];
			
			var typeInfo = GetTypeCode(this.videoTypeList[this.videoType], videoInfo.data.stream);
			if (!string.IsNullOrEmpty(typeInfo.videoTypeCode)) {
				ep = WebUtility.UrlEncode(Convert.ToBase64String(this.RC4("bf7e5f01", sid + "_" + typeInfo.videoTypeCode + "_" + token)));
				fileUrl = this.fileUrlPrefix 
					+ "&ep=" + ep 
					+ "&oip=" + oip
					+ "&sid=" + sid 
					+ "&token=" + token 
					+ "&vid=" + vid 
					+ "&type=" + typeInfo.videoType
					+ "&ts=" + this.now;
			}
		}
		
		return fileUrl;
	}
	
	private string[] GetFileList(string fileUrl) {
		var pageBody = client.Get(fileUrl, null).AsString();
		var data = FormatList(pageBody);
		return data;
	}
	
	private string[] FormatList(string data) {
		var result = new List<string>();

		foreach(Match m in Regex.Matches(data, @"(.*)\.ts\?", RegexOptions.Multiline)) {
			string url = m.ToString();
			
			if (!result.Contains(url))
				result.Add(url);
		}
		return result.ToArray();
	}
	
	private TypeCode GetTypeCode(string videoType, VideoInfoStream[] data) {
		var typeCode = "";

		foreach(var info in data) {
			if (info.stream_type == videoType) {
				typeCode = info.stream_fileid;
				$"file: {info.size:N0} bytes, pixel: {info.width}*{info.height}, seg: {info.segs.Length}".Dump();
			}
		}
		
		if (string.IsNullOrEmpty(typeCode))
			data.Dump();
		
		string typeName;
		switch(videoType) {
			case "mp4hd": typeName = "mp4"; break;
			case "mp4hd2": typeName = "hd2"; break;
			case "mp4hd3": typeName = "hd3"; break;
			default: typeName = "mp4"; break;
		}

		var result = new TypeCode() {
			videoType = typeName,
			videoTypeCode = typeCode
		};
		
		return result;
	}
	
	private byte[] RC4(string a, string c) {
		return RC4(a, Encoding.ASCII.GetBytes(c));
	}
	
	private byte[] RC4(string a, byte[] c)
	{
		int f = 0, i = 0, h = 0;
		List<byte> e = new List<byte>();
		var b = Enumerable.Range(0, 256).ToArray();
		for (h = 0; h < 256; h++)
		{
			f = (f + b[h] + (int)a[h % a.Length]) % 256;
			//swap
			i = b[h];
			b[h] = b[f];
			b[f] = i;
		}
	
		f = h = 0;
	
		for (int q = 0; q < c.Length; q++)
		{
			h = (h + 1) % 256;
			f = (f + b[h]) % 256;
			//swap
			i = b[h];
			b[h] = b[f];
			b[f] = i;
	
			e.Add((byte)(c[q] ^ b[(b[h] + b[f]) % 256]));
		}
		return e.ToArray();
	}

	public struct TypeCode {
		public string videoType;
		public string videoTypeCode;
	}
}