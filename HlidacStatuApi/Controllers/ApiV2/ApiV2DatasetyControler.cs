﻿using HlidacStatu.Datasets;
using HlidacStatu.Entities;
using HlidacStatuApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;

namespace HlidacStatuApi.Controllers.ApiV2
{
    [SwaggerTag("Datasety")]
    [Route("api/v2/datasety")]
    public class ApiV2DatasetyController : ControllerBase
    {
        /// <summary>
        /// Načte seznam datasetů
        /// </summary>
        /// <returns>Seznam datasetů</returns>
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<SearchResultDTO<Registration>>> GetAll()
        {

            DataSet[] result = null;
            if (HttpContext.User.IsInRole("Admin"))
                result = DataSetDB.AllDataSets.Get();
            else
                result = DataSetDB.ProductionDataSets.Get();

            var dataTasks = result.Select(m => m.RegistrationAsync());
            var data = await Task.WhenAll(dataTasks);

            return new SearchResultDTO<Registration>(result.Length, 1, data);
        }

        /// <summary>
        /// Detail konkrétního datasetu
        /// </summary>
        /// <param name="datasetId">Id datasetu (můžeme ho získat ze seznamu datasetů)</param>
        /// <returns>Detail datasetu</returns>
        //[Authorize]
        [Authorize]
        [HttpGet("{datasetId}")]
        public async Task<ActionResult<Registration>> Detail(string datasetId)
        {
            if (string.IsNullOrWhiteSpace(datasetId))
            {
                return BadRequest($"Hodnota id chybí.");
            }

            var ds = DataSet.CachedDatasets.Get(datasetId);
            if (ds == null)
            {
                return NotFound($"Dataset {datasetId} nenalezen.");
            }

            return await ds.RegistrationAsync();
        }

        /// <summary>
        /// Vyhledávání v položkách datasetu
        /// </summary>
        /// <param name="datasetId">Id datasetu (můžeme ho získat ze seznamu datasetů)</param>
        /// <param name="dotaz">Hledaný výraz</param>
        /// <param name="strana">Stránkování</param>
        /// <param name="sort">Název pole pro řazení</param>
        /// <param name="desc">Řazení: 0 - Vzestupně; 1 - Sestupně</param>
        /// <returns></returns>
        [Authorize]
        [HttpGet("{datasetId}/hledat")]
        public async Task<ActionResult<SearchResultDTO<object>>> DatasetSearch(string datasetId, [FromQuery] string? dotaz = null, [FromQuery] int? strana = null, [FromQuery] string? sort = null, [FromQuery] string? desc = "0")
        {
            strana ??= 1;
            if (strana < 1)
                strana = 1;
            if (strana * ApiV2Controller.DefaultResultPageSize > ApiV2Controller.MaxResultsFromES)
            {
                return BadRequest($"Hodnota 'strana' nemůže být větší než {ApiV2Controller.MaxResultsFromES / ApiV2Controller.DefaultResultPageSize}");
            }

            try
            {
                HttpContext.Items.Add("track_query", dotaz);
                
                var ds = DataSet.CachedDatasets.Get(datasetId?.ToLower());
                if (ds == null)
                {
                    return BadRequest($"Dataset [{datasetId}] nenalezen.");
                }

                bool bDesc = (desc == "1" || desc?.ToLower() == "true");
                var res = await ds.SearchDataAsync(dotaz, strana.Value, ApiV2Controller.DefaultResultPageSize, sort + (bDesc ? " desc" : ""));
                res.Result = res.Result.Select(m => { m.DbCreatedBy = null; return m; });

                return new SearchResultDTO<object>(res.Total, res.Page, res.Result);
            }
            catch (DataSetException dex)
            {
                return BadRequest($"{dex.APIResponse.error.description}");
            }
            catch (Exception ex)
            {
                HlidacStatu.Util.Consts.Logger.Error("Dataset API", ex);
                return BadRequest($"Obecná chyba - {ex.Message}");
            }
        }

        /// <summary>
        /// Vytvoří nový dataset
        /// </summary>
        /// <remarks>
        /// Ukázkový požadavek:
        /// https://raw.githubusercontent.com/HlidacStatu/API/master/v2/create_dataset.example.json
        ///     
        /// </remarks>
        /// <param name="data">Objekt typu Registration</param>
        /// <returns> vrací id vytvořeného datasetu </returns>
        /// <response code="200">Dataset vytvořen</response>
        /// <response code="400">Chyba v datech</response>
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<DSCreatedDTO>> Create([FromBody] Registration data)
        {
            if (!ModelState.IsValid)
            {
                var message = string.Join(Environment.NewLine, ModelState.Values.SelectMany(x => x.Errors).Select(x => x.Exception.Message));
                return BadRequest(message);
            }

            try
            {
                var res = await DataSet.Api.CreateAsync(data, HttpContext.User?.Identity?.Name);

                if (res.valid)
                {
                    var regval = (DataSet)res.value;
                    return new DSCreatedDTO(regval.DatasetId);
                }
                else
                {
                    return BadRequest($"{res.error.description}");
                }
            }
            catch (JsonSerializationException jex)
            {
                return BadRequest($"{jex.Message}");
            }
            catch (DataSetException dse)
            {
                return BadRequest($"{dse.APIResponse.error.description}");
            }
            catch (Exception ex)
            {
                HlidacStatu.Util.Consts.Logger.Error("Dataset API", ex);
                return BadRequest($"Obecná chyba - {ex.Message}");
            }
        }

        /// <summary>
        /// Smazání datasetu
        /// </summary>
        /// <param name="datasetId">Id datasetu (můžeme ho získat ze seznamu datasetů)</param>
        /// <returns>True/False</returns>
        [Authorize]
        [HttpDelete("{datasetId}")]
        public async Task<ActionResult<bool>> Delete(string datasetId)
        {
            try
            {
                if (string.IsNullOrEmpty(datasetId))
                {
                    return BadRequest($"Hodnota id chybí.");
                }

                datasetId = datasetId.ToLower();
                var r = await DataSetDB.Instance.GetRegistrationAsync(datasetId);
                if (r == null)
                {
                    return NotFound($"Dataset nenalezen.");
                }

                if (r.createdBy != null && HttpContext.User?.Identity?.Name?.ToLower() != r.createdBy?.ToLower())
                {
                    return Forbid($"Nejste oprávněn mazat tento dataset.");
                }

                var res = await DataSetDB.Instance.DeleteRegistrationAsync(datasetId, HttpContext.User?.Identity?.Name);
                return res;

            }
            catch (DataSetException dse)
            {
                return BadRequest($"{dse.APIResponse.error.description}");
            }
            catch (Exception ex)
            {
                HlidacStatu.Util.Consts.Logger.Error("Dataset API", ex);
                return BadRequest($"Obecná chyba - {ex.Message}");
            }
        }

        /// <summary>
        /// Update datasetu.
        /// </summary>
        ///
        /// <remarks>
        /// Není možné změnit hodnoty jsonSchema a datasetId. Pokud je potřebuješ změnit, 
        /// musíš datovou sadu smazat a zaregistrovat znovu.
        /// 
        /// Ukázkový požadavek:
        /// https://raw.githubusercontent.com/HlidacStatu/API/master/v2/create_dataset.example.json
        /// 
        /// </remarks>
        /// 
        /// <param name="data">Objekt typu Registration</param>
        /// <returns></returns>
        [Authorize]
        [HttpPut]
        public async Task<ActionResult<Registration>> Update([FromBody] Registration data)
        {
            if (!ModelState.IsValid)
            {
                var message = string.Join(Environment.NewLine, ModelState.Values.SelectMany(x => x.Errors).Select(x => x.Exception.Message));
                BadRequest(message);
            }

            ApiResponseStatus res;
            try
            {
                res = await DataSet.Api.UpdateAsync(data, AuthUser()); //blablablabla HttpContext.User?.Identity?.Name);
            }
            catch (Exception ex)
            {
                HlidacStatu.Util.Consts.Logger.Error("Dataset API", ex);
                return BadRequest($"Obecná chyba - {ex.Message}");
            }

            if (res.valid)
            {
                return (Registration)res.value;
            }

            return BadRequest(new ErrorMessage(res));

        }

        #region items
        /// <summary>
        /// Detail konkrétní položky z datasetu
        /// </summary>
        /// <param name="datasetId">Id datasetu (můžeme ho získat ze seznamu datasetů)</param>
        /// <param name="itemId">Id položky v datasetu, kterou chceme načíst</param>
        /// <returns>Vrací detail položky</returns>
        [Authorize]
        [HttpGet("{datasetId}/zaznamy/{itemId}")]
        public async Task<ActionResult<object>> DatasetItem_Get(string datasetId, string itemId)
        {
            try
            {
                var ds = DataSet.CachedDatasets.Get(datasetId.ToLower());
                var value = await ds.GetDataObjAsync(itemId);
                //remove from item
                if (value == null)
                {
                    return BadRequest($"Zaznam nenalezen.");
                }
                else
                {
                    value.DbCreatedBy = null;
                    return value;

                }
            }
            catch (DataSetException)
            {
                return NotFound($"Dataset nenalezen.");
            }
            catch (Exception ex)
            {
                HlidacStatu.Util.Consts.Logger.Error("Dataset API", ex);
                return BadRequest($"Obecná chyba - {ex.Message}");
            }
        }

        /// <summary>
        /// Vloží nebo updatuje záznam v datasetu
        /// </summary>
        /// <param name="datasetId">Id datasetu</param>
        /// <param name="itemId">Id záznamu</param>
        /// <param name="data">Objekt, který se má vložit, nebo updatovat</param>
        /// <param name="mode">"skip" (default) - pokud záznam existuje, nic se na něm nezmění.
        /// "merge" - snaží se spojit data z obou záznamů.
        /// "rewrite" - pokud záznam existuje, je bez milosti přepsán
        /// </param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("{datasetId}/zaznamy/{itemId}")]
        public async Task<ActionResult<DSItemResponseDTO>> DatasetItem_Update(string datasetId, string itemId,
            [FromBody] object data,
            string? mode = "")
        {

            if (!ModelState.IsValid)
            {
                var message = string.Join(Environment.NewLine, ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
                return BadRequest(message);
            }

            mode = mode.ToLower();
            if (string.IsNullOrEmpty(mode))
            {
                mode = "skip";
            }

            datasetId = datasetId.ToLower();
            try
            {
                var ds = DataSet.CachedDatasets.Get(datasetId);
                if (ds is null)
                {
                    return NotFound($"Dataset nenalezen.");
                }

                var newId = itemId;

                if (mode == "rewrite")
                {
                    newId = await ds.AddDataAsync(data.ToString(), itemId, HttpContext.User?.Identity?.Name, true);
                }
                else if (mode == "merge")
                {
                    if (await ds.ItemExistsAsync(itemId))
                    {
                        var oldObj = Util.CleanHsProcessTypeValuesFromObject(await ds.GetDataAsync(itemId));
                        var newObj = Util.CleanHsProcessTypeValuesFromObject(data.ToString());

                        newObj["DbCreated"] = oldObj["DbCreated"];
                        newObj["DbCreatedBy"] = oldObj["DbCreatedBy"];

                        var diffs = Util.CompareObjects(oldObj, newObj);
                        if (diffs.Count > 0)
                        {
                            oldObj.Merge(newObj,
                                new Newtonsoft.Json.Linq.JsonMergeSettings()
                                {
                                    MergeArrayHandling = Newtonsoft.Json.Linq.MergeArrayHandling.Union,
                                    MergeNullValueHandling = Newtonsoft.Json.Linq.MergeNullValueHandling.Ignore
                                });

                            newId = await ds.AddDataAsync(oldObj.ToString(), itemId, HttpContext.User?.Identity?.Name, true);
                        }
                    }
                    else
                        newId = await ds.AddDataAsync(data.ToString(), itemId, HttpContext.User?.Identity?.Name, true);

                }
                else //skip 
                {
                    if (!(await ds.ItemExistsAsync(itemId)))
                        newId = await ds.AddDataAsync(data.ToString(), itemId, HttpContext.User?.Identity?.Name, true);
                }
                return new DSItemResponseDTO() { id = newId };
            }
            catch (DataSetException dse)
            {
                return BadRequest($"{dse.APIResponse.error.description}");
            }
            catch (Exception ex)
            {
                HlidacStatu.Util.Consts.Logger.Error("Dataset API", ex);
                return BadRequest($"Obecná chyba - {ex.Message}");
            }
        }

        /// <summary>
        /// Hromadné vkládání záznamů
        /// </summary>
        /// 
        /// <remarks>
        /// Pokud záznamy s daným ID existují, tak budou přepsány.
        /// 
        ///     Ukázkový požadavek:  
        ///     
        ///     [
        ///     	{
        ///     		"HsProcessType": "person",
        ///     		"Id": "2",
        ///     		"jmeno": "Ferda",
        ///     		"prijmeni": "Mravenec",
        ///     		"narozeni": "2018-11-13T20:20:39+00:00"
        ///     	},
        ///     	{
        ///     		"HsProcessType": "document",
        ///     		"Id": "broukpytlik",
        ///     		"jmeno": "Brouk",
        ///     		"prijmeni": "Pytlík",
        ///     		"narozeni": "2017-12-10T00:00:00+00:00",
        ///     		"DocumentUrl": "www.hlidacstatu.cz",
        ///     		"DocumentPlainText": null
        ///     	}
        ///     ]        
        ///      
        /// </remarks>
        /// <param name="datasetId">Id datasetu, kam chceme záznamy nahrát</param>
        /// <param name="data">Pole JSON objektů</param>
        /// <returns>Id vložených záznamů</returns>
        [Authorize]
        [HttpPost("{datasetId}/zaznamy/")]
        public async Task<ActionResult<List<DSItemResponseDTO>>> DatasetItem_BulkInsert(string datasetId, [FromBody] object data)
        {
            if (!ModelState.IsValid)
            {
                var message = string.Join(Environment.NewLine, ModelState.Values.SelectMany(x => x.Errors).Select(x => x.Exception.Message));
                return BadRequest(message);
            }

            var ds = DataSet.CachedDatasets.Get(datasetId);
            if (ds is null)
            {
                return NotFound($"Dataset nenalezen.");
            }

            string json = data.ToString();
            List<string> results = new List<string>();
            try
            {
                results = await ds.AddDataBulkAsync(json, "usr");
            }
            catch (DataSetException dse)
            {
                return BadRequest($"{dse.APIResponse.error.description}");
            }
            catch (Exception ex)
            {
                HlidacStatu.Util.Consts.Logger.Error("Dataset API", ex);
                return BadRequest($"Obecná chyba - {ex.Message}");
            }
            return results.Select(i => new DSItemResponseDTO() { id = i }).ToList();
        }


        /// <summary>
        /// Kontrola, jestli záznam existuje v datasetu
        /// </summary>
        /// <param name="datasetId">Id datasetu</param>
        /// <param name="itemId">Id záznamu</param>
        /// <returns>true/false</returns>
        [Authorize]
        [HttpGet("{datasetId}/zaznamy/{itemId}/existuje")]
        public async Task<ActionResult<bool>> DatasetItem_Exists(string datasetId, string itemId)
        {
            try
            {
                var ds = DataSet.CachedDatasets.Get(datasetId.ToLower());
                if (ds is null)
                {
                    return NotFound($"Dataset {datasetId} nenalezen.");
                }
                var value = await ds.ItemExistsAsync(itemId);
                //remove from item
                return value;
            }
            catch (DataSetException)
            {
                return NotFound($"Dataset {datasetId} nenalezen.");
            }
            catch (Exception ex)
            {
                HlidacStatu.Util.Consts.Logger.Error("Dataset API", ex);
                return BadRequest($"Obecná chyba - {ex.Message}");
            }
        }

        #endregion
        
        private ApplicationUser AuthUser()
        {
            ApplicationUser user = ApplicationUser.GetByEmail(HttpContext.User?.Identity?.Name);
            return user;
        }

    }

}
