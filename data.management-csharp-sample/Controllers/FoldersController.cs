﻿/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////


using Autodesk.Forge;
using Autodesk.Forge.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace DataManagementSample.Controllers
{
  public class FoldersController : ApiController
  {
    private string AccessToken
    {
      get
      {
        var cookies = Request.Headers.GetCookies();
        var accessToken = cookies[0].Cookies[0].Value;
        return accessToken;
      }
    }

    [HttpPost]
    [Route("api/forge/folders/uploadObject")]
    public async Task<Object> UploadObject()//[FromBody]UploadObjectModel obj)
    {
      // basic input validation
      HttpRequest req = HttpContext.Current.Request;
      if (string.IsNullOrWhiteSpace(req.Params["href"]))
        throw new System.Exception("Folder href parameter was not provided.");

      if (req.Files.Count != 1)
        throw new System.Exception("Missing file to upload"); // for now, let's support just 1 file at a time

      string href = req.Params["href"];
      string[] idParams = href.Split('/');
      string folderId = idParams[idParams.Length - 1];
      string projectId = idParams[idParams.Length - 3];
      HttpPostedFile file = req.Files[0];

      // save the file on the server
      var fileSavePath = Path.Combine(HttpContext.Current.Server.MapPath("~/App_Data"), file.FileName);
      file.SaveAs(fileSavePath);

      StorageRelationshipsTargetData storageRelData = new StorageRelationshipsTargetData(StorageRelationshipsTargetData.TypeEnum.Folders, folderId);
      CreateStorageDataRelationshipsTarget storageTarget = new CreateStorageDataRelationshipsTarget(storageRelData);
      CreateStorageDataRelationships storageRel = new CreateStorageDataRelationships(storageTarget);
      BaseAttributesExtensionObject attributes = new BaseAttributesExtensionObject(string.Empty, string.Empty, new JsonApiLink(string.Empty), null);
      CreateStorageDataAttributes storageAtt = new CreateStorageDataAttributes(file.FileName, attributes);
      CreateStorageData storageData = new CreateStorageData(CreateStorageData.TypeEnum.Objects, storageAtt, storageRel);
      CreateStorage storage = new CreateStorage(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), storageData);

      ProjectsApi projectApi = new ProjectsApi();
      projectApi.Configuration.AccessToken = AccessToken;
      dynamic storageCreated = null;
      storageCreated = await projectApi.PostStorageAsync(projectId, storage);

      string[] storageIdParams = storageCreated.data.id.split('/');
      var objectName = storageIdParams[storageIdParams.Length - 1];
      string[] bucketIdParams = storageIdParams[storageIdParams.Length - 2].Split(':');
      var bucketKey = bucketIdParams[bucketIdParams.Length - 1];

      // upload the file/object
      ObjectsApi objects = new ObjectsApi();
      objects.Configuration.AccessToken = AccessToken;
      dynamic uploadedObj;
      using (StreamReader streamReader = new StreamReader(fileSavePath))
      {
        uploadedObj = await objects.UploadObjectAsync(bucketKey,
               objectName, (int)streamReader.BaseStream.Length, streamReader.BaseStream,
               "application/octet-stream");
      }

      // cleanup
      File.Delete(fileSavePath);

      return uploadedObj;
    }
  }
}
