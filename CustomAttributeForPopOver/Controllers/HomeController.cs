using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CustomAttributeForPopOver.Models;

namespace CustomAttributeForPopOver.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View(new MainPageModelView
            {
                Author = "Author of the record",
                Title = "Record",
                Description = "Description of the record"
            });
        }

        public ActionResult Index2()
            => View(new MainPageWithMetadataTypeModelView
            {
                Author = "Author of the record",
                Title = "Record",
                Description = "Description of the record"
            });
    }
}