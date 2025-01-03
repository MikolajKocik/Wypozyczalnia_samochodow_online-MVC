﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Wypożyczalnia_samochodów_online.Data;
using Wypożyczalnia_samochodów_online.Models;
using Wypożyczalnia_samochodów_online.Services;

namespace Wypożyczalnia_samochodów_online.Controllers
{
    // Ograniczenie dostępu do tego kontrolera tylko dla administratorów
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService; // Serwis do wysyłania e-maili
        private readonly IWebHostEnvironment _webHostEnvironment; // Dostęp do ścieżek (do zasobów) na serwerze
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, EmailService emailService, IWebHostEnvironment webHostEnvironment, ILogger<AdminController> logger)
        {
            _context = context;
            _emailService = emailService;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // Metoda do generowania raportu dla administratora
        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            try
            {
                // Łączna liczba rezerwacji (wszystkich)
                var totalReservations = await _context.Reservations.CountAsync();

                // Łączny dochód (tylko potwierdzone rezerwacje)
                decimal totalIncome = await _context.Reservations
                    .Where(r => r.IsConfirmed)
                    .SumAsync(r => r.TotalCost);

                // Najpopularniejsze samochody
                var popularCars = await _context.Reservations
                    .Include(r => r.Car)
                    .GroupBy(r => new { r.Car.Id, r.Car.Brand, r.Car.Model })
                    .Select(g => new
                    {
                        g.Key.Brand,
                        g.Key.Model,
                        ReservationCount = g.Count()
                    })
                    .OrderByDescending(g => g.ReservationCount)
                    .Take(5)
                    .ToListAsync();

                // Lista niepotwierdzonych rezerwacji 
                var notConfirmed = await _context.Reservations
                    .Include(r => r.Car)
                    .Where(r => !r.IsConfirmed)
                    .ToListAsync();

                var model = new AdminReportsViewModel
                {
                    TotalReservations = totalReservations,
                    TotalIncome = totalIncome,
                    PopularCars = popularCars.Select(pc => new CarReport
                    {
                        Brand = pc.Brand,
                        Model = pc.Model,
                        ReservationCount = pc.ReservationCount
                    }).ToList(),

                    // Lista rezerwacji, które nie zostały jeszcze potwierdzone
                    NotConfirmedReservations = notConfirmed
                };

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                });
            }
        }

        // Metoda do potwierdzenia rezerwacji
        [HttpPost]
        [ValidateAntiForgeryToken] // token
        public async Task<IActionResult> ConfirmReservation(int reservationId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.User) // Żeby załadować dane użytkownika
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
            {
                return NotFound();
            }

            // Oznacz rezerwację jako potwierdzoną
            reservation.IsConfirmed = true;
            await _context.SaveChangesAsync();

            // === WYSŁANIE EMAILA ===
            // Jeśli IdentityUser ma Email = user@example.com, to:
            // reservation.User.Email -> docelowy adres
            // o ile user.Email nie jest null
            // UWAGA: email musi być prawdziwy, żeby wysłać potwierdzenie
            if (!string.IsNullOrEmpty(reservation.User?.Email))
            {
                var toEmail = reservation.User.Email;
                var subject = "Potwierdzenie rezerwacji";
                var body = $@"
                    <h2>Potwierdzenie rezerwacji</h2>
                    <p>Twoja rezerwacja o ID: {reservation.Id} została potwierdzona.</p>
                    <p>Samochód: {reservation.Car?.Brand} {reservation.Car?.Model}</p>
                    <p>Termin: {reservation.StartDate:dd.MM.yyyy} - {reservation.EndDate:dd.MM.yyyy}</p>
                    <p>Koszt: {reservation.TotalCost:C2}</p>
                    <br />
                    <p>Dziękujemy za skorzystanie z naszej wypożyczalni!</p>
                ";

                try
                {
                    _logger.LogInformation($"Próba wysłania e-maila do {toEmail}");
                    await _emailService.SendEmailAsync(toEmail, subject, body);
                    _logger.LogInformation($"E-mail do {toEmail} został wysłany.");
                }
                catch (Exception ex)
                {
                    // obsługa błędu
                    _logger.LogError($"Błąd wysyłki e-maila: {ex.Message}");
                }
            }

            return RedirectToAction(nameof(Reports));
        }

        // Metoda do tworzenia samochodu
        [HttpPost]
        [ValidateAntiForgeryToken] // token
        public async Task<IActionResult> Create(Car car, IFormFile image)
        {
            if (ModelState.IsValid)
            {
                // Obsługuje przesyłanie pliku obrazu
                if (image != null && image.Length > 0)
                {
                    // Generowanie ścieżki do zapisu pliku
                    var uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "Images");
                    var fileName = Path.GetFileName(image.FileName);
                    var filePath = Path.Combine(uploadDir, fileName);

                    // Zapisywanie pliku na serwerze
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(fileStream);
                    }

                    // Przypisanie ścieżki do modelu
                    car.ImageUrl = "/Images/" + fileName;
                }

                // Dodawanie samochodu do bazy danych
                _context.Add(car);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index)); // Po zapisaniu samochodu przekierowanie do listy samochodów
            }
            return View(car);
        }
    }

}

