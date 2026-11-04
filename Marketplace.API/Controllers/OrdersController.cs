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
    //[HttpGet]
    //public IActionResult GetAll()
    //{
    //    var query = _context.Orders.Include(o => o.Items).AsQueryable();

    //    if (!IsAdmin)
    //        query = query.Where(o => o.CustomerId == CurrentUserId);

    //    return Ok(query.ToList());
    //}

    // GET ORDERS, you only see your own (Admin sees all)
    [HttpGet]
    public IActionResult GetAll()
    {
        var query = _context.Orders.Include(o => o.Items).AsQueryable();

        if (!IsAdmin)
            query = query.Where(o => o.CustomerId == CurrentUserId);

        // newest first
        var orders = query.ToList().OrderByDescending(o => o.OrderDate).ToList();

        // 1. collect every product id used in these orders
        var productIds = orders
            .SelectMany(o => o.Items)
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        // 2. look them all up in the catalog in ONE query: id -> name
        var names = _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.Name);

        // 3. write the name onto each item
        var result = orders.Select(o => new
        {
            o.Id,
            o.CustomerId,
            o.OrderDate,
            o.Status,
            Items = o.Items.Select(i => new
            {
                i.Id,
                i.OrderId,
                i.ProductId,
                ProductName = names.TryGetValue(i.ProductId, out var n)
                    ? n
                    : "Deleted product",   // product was deleted later
                i.Quantity,
                i.Price
            })
        });

        return Ok(result);
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