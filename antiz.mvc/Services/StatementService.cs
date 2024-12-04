﻿using BitMinistry;
using BitMinistry.Data.Wrapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using static antiz.mvc.ProfileVm;

namespace antiz.mvc
{
    public class StatementService
    {

        public const int PageSize = 22;

        public vStatementWithStats vStatementWithStats(int statementId, int loginId ) => 
            @$"StatementId = {statementId}
                and ISNULL(LikeUserId, {loginId}) = {loginId}
                and ISNULL(RepostUserId, {loginId}) = {loginId}".QueryForEntity<vStatementWithStats>().FirstOrDefault();

        public StatementListVm GetStatementListVm(int loginId, string sqlProc, int? loadMoreFrom, params ValueTuple<string, object>[] additionalParams )
        {
            var model = new StatementListVm()
            {
                LoadMoreFrom = loadMoreFrom,
                AddPostVm = new AddPostVm()
            };

            if (loadMoreFrom != null)
                using (var sql = new BSqlCommander(comType: CommandType.StoredProcedure))
                {
                    sql.AddWithValue("@login", loginId);
                    sql.AddWithValue("@offSet", loadMoreFrom.Value);
                    sql.AddWithValue("@pageSize", PageSize);
                    foreach (var param in additionalParams)
                        sql.AddWithValue(param.Item1, param.Item2);

                    model.Content = sql.QueryForSql<vStatementWithStats>(sqlProc, reset: false);

                }
            return model;
        }

        static Regex _urlRex = new Regex(@"((http|https|ftp)://[^\s/$.?#].[^\s]*)|www\.[^\s/$.?#].[^\s]*");
        static HashSet<string> _videoProviders = new HashSet<string>(new[] { "youtube.com", "tiktok.com", "x.com", "twitter.com", "facebook.com", "instagram.com", "rumble.com", "vimeo.com" });

        public Statement Post( Statement stm ) {
            stm.RenderedMessage = stm.Message.NewLineToBR();
            var notYetEmbedded = true;

            foreach (var m in _urlRex.Matches(stm.Message).Cast<Match>().Reverse())
            {                
                var uri = new Uri(m.Value.StartsWith("www.") ? "http://" + m.Value : m.Value);

                var replacement = "";

                var hostTmp = uri.Host.Split('.').Reverse().ToArray();
                if (hostTmp.Length > 1)
                {
                    var host2ndLvl = $"{hostTmp[1]}.{hostTmp[0]}";

                    if (notYetEmbedded && _videoProviders.Contains(host2ndLvl))
                    {
                        var social = GenerateEmbedHtml(uri.AbsoluteUri);
                        replacement = social.Item1;
                        stm.SocialNet = social.Item2.Value;
                    }
                    else
                        replacement = $"<a href='{uri.AbsoluteUri}' target=_blank>{uri.Host + uri.LocalPath}</a>";
                }

                stm.RenderedMessage = stm.RenderedMessage.Replace(m.Value, replacement);
            }
            stm.SaveOrUpdate( );
            return stm;
        }

        public (string, SocialNet?) GenerateEmbedHtml(string videoUrl)
        {
            var host = new Uri(videoUrl).Host;
            var framed = "<p>Unsupported video provider.</p>"; string noFrame = null;

            SocialNet? socialNet = null;

            if (host.Contains("youtube.com") || host.Contains("youtu.be"))
            {
                string videoId = videoUrl.Contains("v=")
                    ? videoUrl.Split(new[] { "v=" }, StringSplitOptions.None)[1].Split('&')[0]
                    : videoUrl.Split('/').Last();
                framed = $@"
                <iframe src='https://www.youtube.com/embed/{videoId}' 
                        title='YouTube video player' 
                        frameborder='0' 
                        allow='accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture' 
                        allowfullscreen>
                </iframe>";
                socialNet = SocialNet.YouTube;
            }
            else if (host.Contains("tiktok.com"))
            {
                var videoId = videoUrl.Split('/').Last().Split('?').First();
                noFrame = $@"<div class=tiktok-container>
<iframe src='https://www.tiktok.com/player/v1/{videoId}?&description=1' allow='fullscreen'></iframe>
</div>";
                socialNet = SocialNet.TikTok;

            }
            else if (host.Contains("facebook.com"))
            {
                framed =  $@"
                <div class='fb-video' data-href='{videoUrl}' data-width='auto' data-show-text='false'></div>";
                socialNet = SocialNet.Facebook;
            }
            else if (host.Contains("instagram.com"))
            {
                noFrame =  $@"
                <blockquote class='instagram-media' 
                            data-instgrm-permalink='{videoUrl}' 
                            data-instgrm-version='14' 
                            style='background:#FFF; border:0; border-radius:3px; 
                                   box-shadow:0 0 1px 0 rgba(0,0,0,0.5), 0 1px 10px 0 rgba(0,0,0,0.15); 
                                   margin: 1px; max-width:540px; min-width:326px; padding:0; width:99.375%;'>
                </blockquote>";
                socialNet = SocialNet.Instagram;
            }
            else if (host.Contains("rumble.com"))
            {
                string videoId = videoUrl.Split('/').Last();
                framed =  $@"
                <iframe src='https://rumble.com/embed/{videoId}/' 
                        frameborder='0' 
                        allow='autoplay' 
                        allowfullscreen>
                </iframe>";
                socialNet = SocialNet.Rumble;
            }
            else if (host.Contains("twitter.com") || host.Contains("x.com"))
            {
                noFrame =  $@"
                <blockquote class='twitter-tweet' data-theme=dark>
                    <a href='{videoUrl.Replace("https://x.com", "https://twitter.com")}'></a>
                </blockquote>";
                socialNet = SocialNet.Twitter;
            }
            else if (host.Contains("vimeo.com"))
            {
                string videoId = videoUrl.Split('/').Last();
                framed =  $@"            
                <iframe src='https://player.vimeo.com/video/{videoId}' 
                        frameborder='0' 
                        allow='autoplay; fullscreen' 
                        allowfullscreen>
                </iframe>";
                socialNet = SocialNet.Vimeo;
            }

            if (socialNet == null)
                throw new InvalidOperationException("link was supposed to have social content; but did not");

            return ( 
                ( noFrame ?? $"<div class='social-container'>{framed}</div>" ), 
                socialNet
                );
        }




    }
}