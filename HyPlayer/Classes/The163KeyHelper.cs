#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ATL;

#endregion

namespace HyPlayer.Classes;

internal static class The163KeyHelper
{
    private static readonly Aes _aes = Create163Aes();

    private static Aes Create163Aes()
    {
        var aes = Aes.Create();
        aes.BlockSize = 128;
        aes.Key = Encoding.UTF8.GetBytes(@"#14ljk_!\]&0U<'(");
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }

    /// <summary>
    ///     尝试获取网易云音乐ID
    /// </summary>
    /// <param name="theTrack"></param>
    /// <param name="trackId"></param>
    /// <returns></returns>
    public static bool TryGetTrackId(Track theTrack, out int trackId)
    {
        if (theTrack is null)
            throw new ArgumentNullException(nameof(theTrack));

        trackId = 0;
        var the163Key = theTrack.Comment;
        if (!Is163KeyCandidate(the163Key))
            the163Key = theTrack.Description;
        if (!Is163KeyCandidate(the163Key))
            return false;
        try
        {
            TryGetTrackId(the163Key, out trackId);
        }
        catch
        {
            return false;
        }

        return true;
    }


    public static bool TryGetTrackId(string the163Key, out int trackId)
    {
        if (string.IsNullOrEmpty(the163Key))
            throw new ArgumentNullException(nameof(the163Key));
        trackId = 0;
        try
        {
            the163Key = the163Key.Substring(22);
            var byt163Key = Convert.FromBase64String(the163Key);
            using (var cryptoTransform = _aes.CreateDecryptor())
            {
                byt163Key = cryptoTransform.TransformFinalBlock(byt163Key, 0, byt163Key.Length);
            }

            trackId = (int)JObject.Parse(Encoding.UTF8.GetString(byt163Key).Substring(6))["musicId"];
        }
        catch
        {
            return false;
        }

        return true;
    }

    public static bool TryGetMusicInfo(Track theTrack, out The163KeyClass KeyStruct)
    {
        if (theTrack is null)
            throw new ArgumentNullException(nameof(theTrack));

        KeyStruct = new The163KeyClass();
        var the163Key = theTrack.Comment;
        if (!Is163KeyCandidate(the163Key))
            the163Key = theTrack.Description;
        if (!Is163KeyCandidate(the163Key))
            return false;
        try
        {
            TryGetMusicInfo(the163Key, out KeyStruct);
        }
        catch
        {
            return false;
        }

        return true;
    }

    public static bool TryGetMusicInfo(string the163Key, out The163KeyClass KeyStruct)
    {
        if (string.IsNullOrEmpty(the163Key))
            throw new ArgumentNullException(nameof(the163Key));
        KeyStruct = new The163KeyClass();
        try
        {
            the163Key = the163Key.Substring(22);
            var byt163Key = Convert.FromBase64String(the163Key);
            using (var cryptoTransform = _aes.CreateDecryptor())
            {
                byt163Key = cryptoTransform.TransformFinalBlock(byt163Key, 0, byt163Key.Length);
            }

            KeyStruct = JsonConvert.DeserializeObject<The163KeyClass>(Encoding.UTF8.GetString(byt163Key)
                .Substring(6));
        }
        catch
        {
            return false;
        }

        return true;
    }

    public static bool TrySetMusicInfo(Track theTrack, The163KeyClass key)
    {
        try
        {
            var enc = "music:" + JsonConvert.SerializeObject(key);
            var toEncryptArray = Encoding.UTF8.GetBytes(enc);
            byte[] resultArray;
            using (var cryptoTransform = _aes.CreateEncryptor())
            {
                resultArray = cryptoTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            }

            theTrack.Description = "163 key(Don't modify):" + Convert.ToBase64String(resultArray, 0, resultArray.Length);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     尝试设置163音乐信息到文件
    /// </summary>
    /// <param name="theTrack"></param>
    /// <param name="trackId"></param>
    /// <returns></returns>
    public static bool TrySetMusicInfo(Track theTrack, PlayItem pi)
    {
        if (theTrack is null)
            throw new ArgumentNullException(nameof(theTrack));

        try
        {
            var key = new The163KeyClass
            {
                album = pi.Album.name,
                albumId = int.Parse(pi.Album.id),
                albumPic = pi.Album.cover,
                bitrate = pi.Bitrate,
                artist = null,
                duration = pi.LengthInMilliseconds,
                musicId = int.Parse(pi.Id),
                musicName = pi.Name,
                format = pi.SubExt.ToLower()
            };
            key.artist = pi.Artist.Select(t => new List<object> { t.name, int.Parse(t.id) }).ToList();
            return TrySetMusicInfo(theTrack, key);
        }
        catch
        {
            return false;
        }
    }

    public static string Get163Key(Track theTrack)
    {
        var the163Key = theTrack.Comment;
        if (!Is163KeyCandidate(the163Key))
            the163Key = theTrack.Description;
        if (!Is163KeyCandidate(the163Key))
            return null;
        return the163Key;
    }

    private static bool Is163KeyCandidate(string s)
    {
        return !string.IsNullOrEmpty(s) && s.StartsWith("163 key(Don't modify):", StringComparison.Ordinal);
    }
}

internal class The163KeyClass
{
    public int albumId { get; set; }

    //public string[] alias { get; set; }
    public string album { get; set; }
    public int musicId { get; set; }
    public string musicName { get; set; }
    public double duration { get; set; }
    public int bitrate { get; set; }
    public string albumPic { get; set; }
    public List<List<object>> artist { get; set; }
    public string format { get; set; }
}