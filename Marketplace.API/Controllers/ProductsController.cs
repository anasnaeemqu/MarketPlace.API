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

    // The logged-in user's id, read from the token (never from the request body)
    private string CurrentUserId =>
        User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

    // Empty is fine. Otherwise it must be a real http or https link.
    private static bool IsValidImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;

        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    // GET ALL PRODUCTS, everyone can see them (newest first)
    [HttpGet]
    public IActionResult GetAll()
    {
        var products = _context.Products
            .OrderByDescending(p => p.Id)
            .ToList();
        return Ok(products);
    }

    // GET MY PRODUCTS, only the logged-in vendor's items
    [Authorize(Roles = "Vendor,Admin")]
    [HttpGet("mine")]
    public IActionResult GetMine()
    {
        var me = CurrentUserId;
        var products = _context.Products
            .Where(p => p.VendorId == me)
            .OrderByDescending(p => p.Id)
            .ToList();
        return Ok(products);
    }

    // ADD PRODUCT, Vendors and Admins
    [Authorize(Roles = "Vendor,Admin")]
    [HttpPost]
    public IActionResult AddProduct(Product product)
    {
        if (product.Price < 0 || product.Stock < 0)
            return BadRequest("Price and stock cannot be negative");

        if (!IsValidImageUrl(product.ImageUrl))
            return BadRequest("Image link must start with http:// or https://");

        product.Id = 0;                          // the database picks the id
        product.VendorId = CurrentUserId;        // owner comes from the token
        product.ImageUrl = string.IsNullOrWhiteSpace(product.ImageUrl)
            ? null
            : product.ImageUrl.Trim();

        _context.Products.Add(product);
        _context.SaveChanges();
        return Ok(product);
    }

    // UPDATE PRODUCT, the owner Vendor or an Admin
    [Authorize(Roles = "Vendor,Admin")]
    [HttpPut("{id}")]
    public IActionResult UpdateProduct(int id, Product updated)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
            return NotFound("Product not found");

        if (!User.IsInRole("Admin") && product.VendorId != CurrentUserId)
            return Forbid();

        if (updated.Price < 0 || updated.Stock < 0)
            return BadRequest("Price and stock cannot be negative");

        if (!IsValidImageUrl(updated.ImageUrl))
            return BadRequest("Image link must start with http:// or https://");

        product.Name = updated.Name;
        product.Description = updated.Description;
        product.Price = updated.Price;
        product.Stock = updated.Stock;
        product.ImageUrl = string.IsNullOrWhiteSpace(updated.ImageUrl)
            ? null
            : updated.ImageUrl.Trim();

        _context.SaveChanges();
        return Ok(product);
    }

    // DELETE PRODUCT, the owner Vendor or an Admin
    [Authorize(Roles = "Vendor,Admin")]
    [HttpDelete("{id}")]
    public IActionResult DeleteProduct(int id)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
            return NotFound("Product not found");

        if (!User.IsInRole("Admin") && product.VendorId != CurrentUserId)
            return Forbid();

        _context.Products.Remove(product);
        _context.SaveChanges();
        return Ok("Product deleted");
    }
}