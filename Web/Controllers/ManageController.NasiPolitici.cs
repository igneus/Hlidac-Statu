﻿using HlidacStatu.Entities;
using HlidacStatu.Repositories;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Collections.Generic;
using System.Linq;

namespace HlidacStatu.Web.Controllers
{
    [Authorize]
    public partial class ManageController : Controller
    {

        // find people
        [Authorize(Roles = "NasiPoliticiAdmin")]
        public ActionResult FindPerson(string jmeno, string narozeni, bool? extendSearch)
        {
            // šlo by urychlit pomocí full text searche
            var osoby = OsobaRepo.Searching.FindAll(jmeno, narozeni, extendSearch.GetValueOrDefault(false));

            return View(osoby);
        }

        [Authorize(Roles = "NasiPoliticiAdmin")]
        public ActionResult PersonDetail(int? id, string columOrdering, int? descending)
        {
            if (id == null) return RedirectToAction(nameof(FindPerson));

            Osoba osoba = OsobaRepo.GetByInternalId(id ?? 0);

            return View(osoba);
        }

        [Authorize(Roles = "canEditData")]
        public ActionResult CompanyDetail(string ico, string columOrdering, int? descending)
        {
            if (string.IsNullOrWhiteSpace(ico))
                return NotFound();

            Firma firma = FirmaRepo.FromIco(ico);

            if (firma is null || !firma.Valid)
                return NotFound();


            return View(firma);
        }


        [Authorize(Roles = "NasiPoliticiAdmin")]
        public ActionResult CreatePerson()
        {
            Osoba osoba = new Osoba();
            return View(osoba);
        }

        // Create a new person
        [Authorize(Roles = "NasiPoliticiAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreatePerson(Osoba osoba)
        {
            if (ModelState.IsValid)
            {
                var person = OsobaRepo.GetOrCreateNew(osoba.TitulPred, osoba.Jmeno, osoba.Prijmeni,
                    osoba.TitulPo, osoba.Narozeni, (Osoba.StatusOsobyEnum)osoba.Status,
                    this.User.Identity.Name, osoba.Umrti);

                return RedirectToAction(nameof(FindPerson),
                    new
                    {
                        jmeno = $"{osoba.Jmeno} {osoba.Prijmeni}",
                        narozeni = osoba.Narozeni?.ToString("dd.MM.yyyy")
                    });
            }

            return View(osoba);
        }

        [Authorize(Roles = "NasiPoliticiAdmin")]
        public ActionResult EditPersonNP(int? id)
        {
            if (id == null) return RedirectToAction(nameof(FindPerson));

            Osoba osoba = OsobaRepo.GetByInternalId(id ?? 0);

            return View(osoba);
        }

        [Authorize(Roles = "NasiPoliticiAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditPersonNP(int id, [Bind("InternalId,TitulPred,Jmeno,Prijmeni,TitulPo,Narozeni,Umrti,Status,WikiId")] Osoba osoba)
        {

            if (id != osoba.InternalId) return RedirectToAction(nameof(FindPerson));
            if (ModelState.IsValid)
            {
                Osoba result = OsobaRepo.Update(osoba, this.User.Identity.Name);

                return RedirectToAction(nameof(FindPerson),
                    new
                    {
                        jmeno = $"{osoba.Jmeno} {osoba.Prijmeni}",
                        narozeni = osoba.Narozeni?.ToString("dd.MM.yyyy")
                    });
            }

            return View(osoba);
        }

        [Authorize(Roles = "NasiPoliticiAdmin")]
        public ActionResult SetTimestamp(int id)
        {
            OsobaRepo.SetManualTimeStamp(id, this.User.Identity.Name);
            return RedirectToAction(nameof(PersonDetail), new { id });
        }

        [Authorize(Roles = "NasiPoliticiAdmin")]
        [HttpPost]
        public JsonResult UpdateEvent(OsobaEvent osobaEvent)
        {
            if (ModelState.IsValid)
            {
                var result = OsobaEventRepo.CreateOrUpdate(osobaEvent, this.User.Identity.Name);
                return Json(new { Success = true });
            }

            var errorModel = GetModelErrorsJSON();

            Response.StatusCode = 400;
            return new JsonResult(new { Data = errorModel });
        }

        [Authorize(Roles = "NasiPoliticiAdmin")]
        [HttpPost]
        public JsonResult DeleteEvent(int? id)
        {
            if ((id ?? 0) == 0)
            {
                Response.StatusCode = 400;
                return new JsonResult(new { Data = "chybí id" });
            }
            OsobaEvent osobaEvent = OsobaEventRepo.GetById(id ?? 0);
            if (osobaEvent is null)
            {
                Response.StatusCode = 400;
                return new JsonResult(new { Data = "Event neexistuje." });
            }
            OsobaEventRepo.Delete(osobaEvent, this.User.Identity.Name);
            return Json(new { Success = true });
        }


        private IEnumerable<ErrorModel> GetModelErrorsJSON()
        {
            return from x in ModelState.Keys
                   where ModelState[x].Errors.Count > 0
                   select new ErrorModel()
                   {
                       key = x,
                       errors = ModelState[x].Errors.Select(y => y.ErrorMessage).ToArray()
                   };
        }

        private class ErrorModel
        {
            public string key { get; set; }
            public string[] errors { get; set; }
        }
    }
}