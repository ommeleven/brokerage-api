using Microsoft.AspNetCore.Mvc;
using BrokerApi.Models;

namespace BrokerApi.Controllers;

[ApiController]
[Route("api/[controller]")] // route here is "api/accounts"
public class AccountsController : ControllerBase
{
    private static readonly List<Account> _accounts = new()
    {
        new Account("A1", "Alice"),
        new Account("A2", "Bob")
    };

    [HttpGet]
    public IEnumerable<Account> GetAll()=> _accounts;

    [HttpGet("{id}")]

    public ActionResult<Account> GetById(string id)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == id);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPost]
    public ActionResult<Account> CreateAccount(Account account)
    {
        _accounts.Add(account);
        return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
    }
}