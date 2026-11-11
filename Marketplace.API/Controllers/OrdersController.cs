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
        if (order.Items == null || order.Items.Count == 0)
            return BadRequest("Order must contain at least one item");

        foreach (var item in order.Items)
        {
            if (item.Quantity <= 0)
                return BadRequest("Quantity must be at least 1");

            // Rule 1: look at the shelf
            var product = _context.Products.FirstOrDefault(p => p.Id == item.ProductId);

            if (product == null)
                return BadRequest("Product not found");

            // Rule 3: not enough? say no
            if (product.Stock < item.Quantity)
                return BadRequest($"Only {product.Stock} left of {product.Name}");

            // Rule 2: sell it and take it off the shelf
            product.Stock -= item.Quantity;

            // Use the real price from the database, not the one Flutter sent
            item.Price = product.Price;
        }

        order.CustomerId = CurrentUserId;
        order.OrderDate = DateTime.UtcNow;
        order.Status = "Pending";

        _context.Orders.Add(order);
        _context.SaveChanges();   // saves the order AND the lower stock together
        return Ok(order);
    }

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

    // UPDATE ORDER STATUS, Admin only, one step forward at a time
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}/status")]
    public IActionResult UpdateStatus(int id, UpdateStatusRequest request)
    {
        var order = _context.Orders.FirstOrDefault(o => o.Id == id);
        if (order == null)
            return NotFound("Order not found");

        var steps = new[] { "Pending", "Shipped", "Delivered" };
        var from = Array.IndexOf(steps, order.Status);
        var to = Array.IndexOf(steps, request.Status);

        if (to == -1)
            return BadRequest("Status must be Pending, Shipped or Delivered");

        if (from == -1)
            return BadRequest($"Order is {order.Status} and cannot be changed");

        if (to != from + 1)
            return BadRequest($"Cannot move from {order.Status} to {request.Status}");

        order.Status = request.Status;
        _context.SaveChanges();
        return Ok(new { order.Id, order.Status });
    }

    // CANCEL ORDER, owner (or Admin), Pending only, stock goes back
    [Authorize(Roles = "Customer,Admin")]
    [HttpPut("{id}/cancel")]
    public IActionResult CancelOrder(int id)
    {
        var order = _context.Orders
            .Include(o => o.Items)
            .FirstOrDefault(o => o.Id == id);

        if (order == null)
            return NotFound("Order not found");

        if (!IsAdmin && order.CustomerId != CurrentUserId)
            return Forbid();

        if (order.Status != "Pending")
            return BadRequest($"Only Pending orders can be cancelled (this one is {order.Status})");

        // put the items back on the shelf
        foreach (var item in order.Items)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null)
                product.Stock += item.Quantity;
        }

        order.Status = "Cancelled";
        _context.SaveChanges();   // status and stock saved together
        return Ok(new { order.Id, order.Status });
    }
}