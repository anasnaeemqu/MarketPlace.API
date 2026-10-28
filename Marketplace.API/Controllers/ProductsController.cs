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

        product.Name = updated.Name;
        product.Description = updated.Description;
        product.Price = updated.Price;
        product.Stock = updated.Stock;

        _context.SaveChanges();
        return Ok(product);
    }

    // DELETE PRODUCT — Only Admins
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public IActionResult DeleteProduct(int id)
    {
        var product = _context.Products.FirstOrDefault(p => p.Id == id);

        if (product == null)
            return NotFound("Product not found");

        _context.Products.Remove(product);
        _context.SaveChanges();
        return Ok("Product deleted");
    }
}