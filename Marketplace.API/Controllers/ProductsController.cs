using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.API.Infrastructure;
using Marketplace.API.Domain;
using System.Security.Claims;

namespace Marketplace.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    // GET ALL PRODUCTS — Everyone can see
    [HttpGet]
    public IActionResult GetAll()
    {
        var products = _context.Products.ToList();
        return Ok(products);
    }

    // ADD PRODUCT — Only Vendors and Admins

    [Authorize(Roles = "Vendor,Admin")]
    [HttpPost]
    public IActionResult AddProduct(Product product)
    {
        product.VendorId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value; // 👈
        _context.Products.Add(product);
        _context.SaveChanges();
        return Ok(product);
    }

    // UPDATE PRODUCT — Only Vendors and Admins
    [Authorize(Roles = "Vendor,Admin")]
    [HttpPut("{id}")]
    public IActionResult UpdateProduct(int id, Product updated)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);

        if (product == null)
            return NotFound("Product not found");

        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        // Not the owner and not an Admin? Stop.
        if (!User.IsInRole("Admin") && product.VendorId != currentUserId)
            return Forbid();

        product.Name = updated.Name;
        product.Description = updated.Description;
        product.Price = updated.Price;
        product.Stock = updated.Stock;

        _context.SaveChanges();
        return Ok(product);
    }

    // DELETE PRODUCT — Only Admins
    [Authorize(Roles = "Vendor,Admin")]
    [HttpDelete("{id}")]
    public IActionResult DeleteProduct(int id)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
            return NotFound("Product not found");

        var me = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        if (!User.IsInRole("Admin") && product.VendorId != me)
            return Forbid();

        _context.Products.Remove(product);
        _context.SaveChanges();
        return Ok("Product deleted");
    }

    // GET MY PRODUCTS, only the logged-in vendor's items
    [Authorize(Roles = "Vendor,Admin")]
    [HttpGet("mine")]
    public IActionResult GetMine()
    {
        var me = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var products = _context.Products.Where(p => p.VendorId == me).ToList();
        return Ok(products);
    }
}