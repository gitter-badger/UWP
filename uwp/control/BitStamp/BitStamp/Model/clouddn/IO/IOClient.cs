﻿using System;
using System.Collections.Generic;
using System.Net;
using Qiniu.Conf;
using Qiniu.Auth;
using Qiniu.RPC;
using Qiniu.Util;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Web.Http;
using Qiniu.Auth.digest;
using Qiniu.RS;

namespace Qiniu.IO
{
    /// <summary>
    /// 上传客户端
    /// </summary>
    public class IOClient
    {
        /// <summary>
        /// 无论成功或失败，上传结束时触发的事件
        /// </summary>
        public event EventHandler<PutRet> PutFinished;

        private static WebHeaderCollection /*NameValueCollection*/ getFormData(string upToken, string key, PutExtra extra)
        {
            //System.Collections.Specialized
            WebHeaderCollection webHeader=new WebHeaderCollection();
            //NameValueCollection formData = new NameValueCollection();
            webHeader["token"] = upToken;
            //formData["token"] = upToken;

            if (key!=null)
            {
                webHeader["key"] = key;
                //formData["key"] = key;
            }
            if (extra != null)
            {
                if (extra.CheckCrc == CheckCrcType.CHECK_AUTO)
                {
                    webHeader["crc32"]= extra.Crc32.ToString();
                    //formData["crc32"] = extra.Crc32.ToString();
                }
                if (extra.Params != null)
                {
                    foreach (KeyValuePair<string, string> pair in extra.Params)
                    {
                        webHeader[pair.Key] = pair.Value;
                        //formData[pair.Key] = pair.Value;
                    }
                }
            }
            return webHeader;
            //return formData;
        }

        /// <summary>
        /// 设置连接代理
        /// </summary>
        public IWebProxy Proxy { get; set; }


		/// <summary>
		/// 上传文件
		/// </summary>
		/// <param name="upToken"></param>
		/// <param name="key"></param>h
		/// <param name="localFile"></param>
		/// <param name="extra"></param>
		public async System.Threading.Tasks.Task<PutRet> PutFile(string upToken, string key, string localFile, PutExtra extra)
		{
			if (!System.IO.File.Exists(localFile))
			{
				throw new Exception(string.Format("{0} does not exist", localFile));
			}
			PutRet ret;

            /*NameValueCollection*/
            WebHeaderCollection formData = getFormData(upToken, key, extra);
			try
			{
				CallRet callRet = await MultiPart.MultiPost(Config.UP_HOST, formData, localFile, this.Proxy);
				ret = new PutRet(callRet);
				onPutFinished(ret);
				return ret;
			}
			catch (Exception e)
			{
				ret = new PutRet(new CallRet(System.Net.HttpStatusCode.BadRequest, e));
				onPutFinished(ret);
				return ret;
			}
		}
		/// <summary>
		/// Puts the file without key.
		/// </summary>
		/// <returns>The file without key.</returns>
		/// <param name="upToken">Up token.</param>
		/// <param name="localFile">Local file.</param>
		/// <param name="extra">Extra.</param>
		public async System.Threading.Tasks.Task<PutRet> PutFileWithoutKey(string upToken, string localFile, PutExtra extra)
		{
			return await PutFile(upToken, null, localFile, extra);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="upToken">Up token.</param>
		/// <param name="key">Key.</param>
		/// <param name="putStream">Put stream.</param>
		/// <param name="extra">Extra.</param>
		public async System.Threading.Tasks.Task<PutRet> Put(string upToken, string key, System.IO.Stream putStream, PutExtra extra)
		{
			if (!putStream.CanRead)
			{
				throw new Exception("read put Stream error");
			}
			PutRet ret;
            /*NameValueCollection*/
            WebHeaderCollection formData = getFormData(upToken, key, extra);
			try
			{
				CallRet callRet = await MultiPart.MultiPost(Config.UP_HOST, formData, putStream);
				ret = new PutRet(callRet);
				onPutFinished(ret);
				return ret;
			}
			catch (Exception e)
			{
				ret = new PutRet(new CallRet(System.Net.HttpStatusCode.BadRequest, e));
				onPutFinished(ret);
				return ret;
			}
		}

		protected void onPutFinished(PutRet ret)
        {
            if (PutFinished != null)
            {
                PutFinished(this, ret);
            }
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="accessKey"></param>
        /// <param name="secretKey"></param>
        /// <param name="bucket"></param>
        /// <param name="file"></param>
        /// <param name="name">文件名</param>
        /// <returns></returns>
        public async Task<PutRet> UploadFile(string accessKey,
            string secretKey,
            string bucket,
            StorageFile file,
            string name = null)
        {
            Mac mac = new Mac(accessKey, Qiniu.Conf.Config.Encoding.GetBytes(secretKey));
            PutPolicy putPolicy = new PutPolicy();
            putPolicy.Scope = bucket;
            putPolicy.SetExpires(3600);

            string uploadToken = mac.CreateUploadToken(putPolicy);

            Stream stream = await file.OpenStreamForReadAsync();

            var ret= await new Qiniu.IO.IOClient().Put(uploadToken, name, stream, new PutExtra());

            stream.Dispose();

            return ret;
        }
    }
}