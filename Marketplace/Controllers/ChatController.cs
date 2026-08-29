using Marketplace.Models;
using Marketplace.Services;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Controllers
{
    // ChatController - handles inbox, threads, and blocking for user messages.
    // All actions require login.
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ChatService _chat;
        private readonly UserManager<ApplicationUser> _userManager;

        // Constructor - wires up chat service and user manager.
        public ChatController(ChatService chat, UserManager<ApplicationUser> userManager)
        {
            _chat = chat;
            _userManager = userManager;
        }

        // Inbox - shows your conversations, paged so it stays fast.
        public async Task<IActionResult> Inbox(int page = 1, int pageSize = 12)
        {
            var meId = _userManager.GetUserId(User) ?? "";

            var vm = await _chat.GetInboxAsync(meId, page, pageSize);

            return View(vm);
        }

        // UnreadCount - returns how many unread messages you have, used for the badge.
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var meId = _userManager.GetUserId(User);

            if (meId == null)
            {
                return Json(new { count = 0 });
            }

            var c = await _chat.GetUnreadCountAsync(meId);

            return Json(new { count = c });
        }

        // Thread - shows the chat thread with another user for a specific ad.
        [HttpGet]
        public async Task<IActionResult> Thread(string with, int adId, int page = 1, int pageSize = 50)
        {
            var meId = _userManager.GetUserId(User) ?? "";

            var amAdmin = User.IsInRole(Helper.AdminRole);

            var meName = User.Identity?.Name ?? "";

            var (outcome, vm) = await _chat.GetThreadAsync(with, adId, meId, meName, amAdmin, page, pageSize);

            if (outcome == ChatService.ThreadOutcome.PartnerNotFound || outcome == ChatService.ThreadOutcome.AdNotFound)
            {
                return NotFound();
            }

            return View(vm);
        }

        // ReportThread - report a chat thread for review, once per thread.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportThread(string with, int adId, string reason, string description)
        {
            var meId = _userManager.GetUserId(User) ?? "";

            if (!Enum.TryParse<ReportReason>(reason, out var parsedReason))
            {
                TempData["ChatError"] = "Please pick a valid reason.";
                return RedirectToAction(nameof(Thread), new { with, adId });
            }

            var outcome = await _chat.ReportThreadAsync(meId, with, adId, parsedReason, description);

            if (outcome == ChatService.ReportOutcome.Ok)
            {
                TempData["ChatNotice"] = "Report sent. An admin will review this chat.";
            }
            else if (outcome == ChatService.ReportOutcome.AlreadyReported)
            {
                TempData["ChatError"] = "You already reported this chat.";
            }
            else if (outcome == ChatService.ReportOutcome.InvalidInput)
            {
                TempData["ChatError"] = "Description must be 20-500 characters.";
            }
            else if (outcome == ChatService.ReportOutcome.SelfReport)
            {
                TempData["ChatError"] = "You cannot report yourself.";
            }
            else if (outcome == ChatService.ReportOutcome.AdminTarget)
            {
                TempData["ChatError"] = "Administrators cannot be reported.";
            }
            else
            {
                TempData["ChatError"] = "Could not report this chat.";
            }

            return RedirectToAction(nameof(Thread), new { with, adId });
        }

        // SendPhone - shares your phone number as a chat message, one click.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPhone(string with, int adId)
        {
            var meId = _userManager.GetUserId(User) ?? "";

            var result = await _chat.SendPhoneAsync(meId, with, adId);

            if (result == ChatService.PhoneShareOutcome.NoPhone)
            {
                TempData["ChatError"] = "Add a phone number in your profile first.";
                return RedirectToAction(nameof(Thread), new { with, adId });
            }

            if (result == ChatService.PhoneShareOutcome.Blocked)
            {
                TempData["ChatError"] = "Messaging is blocked.";
                return RedirectToAction(nameof(Thread), new { with, adId });
            }

            return RedirectToAction(nameof(Thread), new { with, adId });
        }

        // Block - blocks another user so they cannot message you anymore.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Block(string with, int adId)
        {
            var meId = _userManager.GetUserId(User) ?? "";

            var (outcome, partner) = await _chat.BlockAsync(meId, with);

            switch (outcome)
            {
                case ChatService.BlockOutcome.SelfBlock:
                    TempData["ChatError"] = "You cannot block yourself.";

                    break;

                case ChatService.BlockOutcome.AdminTarget:
                    TempData["ChatError"] = "Administrators cannot be blocked.";

                    break;

                case ChatService.BlockOutcome.Ok:
                    if (partner != null)
                    {
                        TempData["ChatNotice"] = $"{partner.UserName} can no longer message you.";
                    }

                    break;
            }

            return RedirectToAction(nameof(Thread), new { with, adId });
        }

        // Unblock - lets a previously blocked user message you again.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unblock(string with, int adId)
        {
            var meId = _userManager.GetUserId(User) ?? "";

            var partner = await _chat.UnblockAsync(meId, with);

            if (partner != null)
            {
                TempData["ChatNotice"] = $"{partner.UserName} can message you again.";
            }

            return RedirectToAction(nameof(Thread), new { with, adId });
        }
    }
}
