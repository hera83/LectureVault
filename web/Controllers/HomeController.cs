using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using web.Data.Entities;
using web.Infrastructure;
using web.Models;
using web.Repositories.Lectures.Dtos;
using web.Repositories.Lectures.Interfaces;
using web.ViewModels;

namespace web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILecturesService _lecturesService;

        public HomeController(UserManager<ApplicationUser> userManager, ILecturesService lecturesService)
        {
            _userManager = userManager;
            _lecturesService = lecturesService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

            var lectures = await _lecturesService.GetLecturesAsync(userId, HttpContext.RequestAborted);

            var vm = new LectureListViewModel
            {
                Lectures = lectures.Select(l => new LectureCardViewModel
                {
                    Id = l.Id,
                    Name = l.Name,
                    FileCount = l.FileCount,
                    CreatedAtUtc = l.CreatedAtUtc
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateLectureViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                this.ToastError(string.IsNullOrWhiteSpace(errors) ? "Lektionen kunne ikke oprettes." : errors);
                return RedirectToAction("Index");
            }

            var result = await _lecturesService.CreateLectureAsync(new CreateLectureRequestDto
            {
                OwnerId = userId,
                Name = model.Name
            }, HttpContext.RequestAborted);

            if (!result.Success)
            {
                this.ToastError(result.ErrorMessage ?? "Lektionen kunne ikke oprettes.");
                return RedirectToAction("Index");
            }

            this.ToastSuccess($"Lektionen '{model.Name}' er oprettet.");
            return RedirectToAction("Details", "Lecture", new { id = result.LectureId });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [AllowAnonymous]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
