using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Marketplace.API.Infrastructure;
using Marketplace.API.Domain;
using System.Security.Claims;

namespace Marketplace.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;

    public OrdersController(AppDbContext context)
    {
        _context = context;
    }

    // Helpers: read who is logged in from the token
    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private bool IsAdmin => User.IsInRole("Admin");

    // PLACE ORDER, Customers and Admins only
    [Authorize(Roles = "Customer,Admin")]
    [HttpPost]
    public IActionResult PlaceOrder(Order order)
    {
        order.CustomerId = CurrentUserId;   // from the token, not from Flutter
        order.OrderDate = DateTime.UtcNow;
        order.Status = "Pending";

        _context.Orders.Add(order);
        _context.SaveChanges();
        return Ok(order);
    }

    // GET ORDERS, you only see your own (Admin sees all)
    [HttpGet]
    public IActionResult GetAll()
    {
        var query = _context.Orders.Include(o => o.Items).AsQueryable();

        if (!IsAdmin)
            query = query.Where(o => o.CustomerId == CurrentUserId);

        return Ok(query.ToList());
    }

    // GET SINGLE ORDER
    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        var order = _context.Orders
            .Include(o => o.Items)
            .FirstOrDefault(o => o.Id == id);

        if (order == null)
            return NotFound("Order not found");

        if (!IsAdmin && order.CustomerId != CurrentUserId)
            return Forbid();

        return Ok(order);
    }
}