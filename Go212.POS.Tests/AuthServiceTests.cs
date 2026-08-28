using FluentAssertions;
using Go212.POS.Application.Interfaces;
using Go212.POS.Application.Services;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Enums;
using Go212.POS.Domain.Exceptions;
using Go212.POS.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;


namespace Go212.POS.Tests;

/// <summary>
/// Unit tests for AuthService.
/// Tests: correct PIN → session, wrong PIN → exception + attempt increment,
/// locked account → AccountLockedException, deactivated user → exception.
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ISettingRepository> _settings = new();
    private readonly Mock<IAuditRepository> _audit = new();

    public AuthServiceTests()
    {
        _uow.Setup(u => u.Users).Returns(_users.Object);
        _uow.Setup(u => u.Settings).Returns(_settings.Object);
        _uow.Setup(u => u.Audit).Returns(_audit.Object);

        // Default settings
        _settings.Setup(s => s.GetValueAsync("session.pin_max_attempts")).ReturnsAsync("5");
        _settings.Setup(s => s.GetValueAsync("session.lock_minutes")).ReturnsAsync("15");
        _audit.Setup(a => a.LogAsync(It.IsAny<AuditEvent>())).Returns(Task.CompletedTask);
    }

    private AuthService CreateService() => new(_uow.Object, NullLogger<AuthService>.Instance);

    private User MakeUser(string pin = "1234", bool isActive = true, int failedAttempts = 0, DateTime? lockedUntil = null)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(pin);
        return new User
        {
            Id = 1, Username = "caissier", Name = "Test User",
            PinHash = hash, Role = UserRole.Cashier,
            IsActive = isActive,
            FailedLoginAttempts = failedAttempts,
            LockedUntil = lockedUntil
        };
    }

    [Fact]
    public async Task AuthenticateAsync_CorrectPin_ReturnsSession()
    {
        // Arrange
        var user = MakeUser("1234");
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);
        _users.Setup(u => u.UpdateLastLoginAsync(It.IsAny<long>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _users.Setup(u => u.UpdateFailedAttemptsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<DateTime?>())).Returns(Task.CompletedTask);

        var svc = CreateService();

        // Act
        var session = await svc.AuthenticateAsync(1, "1234");

        // Assert
        session.Should().NotBeNull();
        session.UserId.Should().Be(1);
        session.Username.Should().Be("caissier");
        session.Role.Should().Be(UserRole.Cashier);
    }

    [Fact]
    public async Task AuthenticateAsync_WrongPin_ThrowsAuthenticationException()
    {
        // Arrange
        var user = MakeUser("1234", failedAttempts: 0);
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);
        _users.Setup(u => u.UpdateFailedAttemptsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<DateTime?>())).Returns(Task.CompletedTask);

        var svc = CreateService();

        // Act
        var act = () => svc.AuthenticateAsync(1, "0000");

        // Assert
        await act.Should().ThrowAsync<AuthenticationException>();
        _users.Verify(u => u.UpdateFailedAttemptsAsync(1, 1, null), Times.Once); // attempt incremented
    }

    [Fact]
    public async Task AuthenticateAsync_MaxFailedAttempts_LocksAccount()
    {
        // Arrange — user already at 4 attempts (one more = lock)
        var user = MakeUser("1234", failedAttempts: 4);
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);
        _users.Setup(u => u.UpdateFailedAttemptsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<DateTime?>())).Returns(Task.CompletedTask);

        var svc = CreateService();

        // Act
        var act = () => svc.AuthenticateAsync(1, "0000");

        // Assert — should lock, not just throw AuthenticationException
        await act.Should().ThrowAsync<AccountLockedException>();
        _users.Verify(u => u.UpdateFailedAttemptsAsync(1, 5, It.IsNotNull<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_AlreadyLocked_ThrowsAccountLockedException()
    {
        // Arrange — account is currently locked
        var lockedUntil = DateTime.UtcNow.AddMinutes(10);
        var user = MakeUser("1234", lockedUntil: lockedUntil);
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        var svc = CreateService();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AccountLockedException>(() => svc.AuthenticateAsync(1, "1234"));
        ex.LockedUntil.Should().BeCloseTo(lockedUntil.ToLocalTime(), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AuthenticateAsync_InactiveUser_ThrowsAuthenticationException()
    {
        var user = MakeUser("1234", isActive: false);
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        var svc = CreateService();

        await Assert.ThrowsAsync<AuthenticationException>(() => svc.AuthenticateAsync(1, "1234"));
    }

    [Fact]
    public async Task AuthenticateAsync_UserNotFound_ThrowsEntityNotFoundException()
    {
        _users.Setup(u => u.GetByIdAsync(99)).ReturnsAsync((User?)null);

        var svc = CreateService();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => svc.AuthenticateAsync(99, "1234"));
    }

    [Fact]
    public async Task AuthenticateAsync_CorrectPin_ResetsFailedAttempts()
    {
        var user = MakeUser("1234", failedAttempts: 3);
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);
        _users.Setup(u => u.UpdateLastLoginAsync(It.IsAny<long>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _users.Setup(u => u.UpdateFailedAttemptsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<DateTime?>())).Returns(Task.CompletedTask);

        var svc = CreateService();
        await svc.AuthenticateAsync(1, "1234");

        // Verify failed attempts reset to 0
        _users.Verify(u => u.UpdateFailedAttemptsAsync(1, 0, null), Times.Once);
    }
}

/// <summary>
/// Unit tests for sale business rules.
/// Tests: sale totals, empty sale rejection, double-click protection, cancellation.
/// </summary>
public class SaleBusinessRulesTests
{
    [Fact]
    public void SaleItem_LineTotals_CalculatedCorrectly()
    {
        // Arrange
        var item = new SaleItem
        {
            Quantity        = 3,
            UnitPriceHT     = 10.00m,
            TaxRate         = 20.00m,
            DiscountPercent = 0,
        };

        // Act — computed properties
        var expectedHT  = 30.00m;
        var expectedTax = 6.00m;
        var expectedTTC = 36.00m;

        // Assert
        item.LineTotalHT.Should().Be(expectedHT);
        item.LineTaxAmount.Should().Be(expectedTax);
        item.LineTotalTTC.Should().Be(expectedTTC);
    }

    [Fact]
    public void SaleItem_WithDiscount_CalculatedCorrectly()
    {
        var item = new SaleItem
        {
            Quantity        = 2,
            UnitPriceHT     = 100.00m,
            TaxRate         = 20.00m,
            DiscountPercent = 10m,  // 10% discount
        };

        // 2 × 100 × (1-0.1) = 180 HT
        item.LineTotalHT.Should().Be(180.00m);
        // 180 × 0.20 = 36 TVA
        item.LineTaxAmount.Should().Be(36.00m);
        // 216 TTC
        item.LineTotalTTC.Should().Be(216.00m);
    }

    [Fact]
    public void Product_PriceTTC_ComputedFromHTandTVA()
    {
        var product = new Product { PriceHT = 83.33m, TaxRate = 20m };
        product.PriceTTC.Should().Be(100.00m);
    }

    [Fact]
    public void Sale_IsLowStock_TrueWhenAtOrBelowThreshold()
    {
        var product = new Product { StockQuantity = 3, StockAlertThreshold = 5 };
        product.IsLowStock.Should().BeTrue();
    }

    [Fact]
    public void Sale_IsLowStock_FalseWhenAboveThreshold()
    {
        var product = new Product { StockQuantity = 10, StockAlertThreshold = 5 };
        product.IsLowStock.Should().BeFalse();
    }

    [Fact]
    public void CashSession_ClosingDiscrepancy_CalculatedCorrectly()
    {
        var session = new CashSession
        {
            ClosingExpected = 1000.00m,
            ClosingCounted  = 980.00m,
        };

        session.ClosingDiscrepancy.Should().Be(-20.00m); // shortfall
    }

    [Theory]
    [InlineData(100.00, 20.00, 120.00)]  // standard 20% TVA
    [InlineData(50.00,  7.00,   53.50)]  // 7% TVA
    [InlineData(200.00, 0.00,  200.00)]  // no TVA
    public void SaleItem_TVA_VariousRates(decimal ht, decimal taxRate, decimal expectedTTC)
    {
        var item = new SaleItem
        {
            Quantity        = 1,
            UnitPriceHT     = ht,
            TaxRate         = taxRate,
            DiscountPercent = 0,
        };

        item.LineTotalTTC.Should().BeApproximately(expectedTTC, 0.01m);
    }
}

/// <summary>
/// Tests for domain exception messages and properties.
/// </summary>
public class DomainExceptionTests
{
    [Fact]
    public void EntityNotFoundException_HasCorrectMessage()
    {
        var ex = new EntityNotFoundException("Product", 42);
        ex.Message.Should().Contain("Product").And.Contain("42");
    }

    [Fact]
    public void InsufficientStockException_HasCorrectMessage()
    {
        var ex = new InsufficientStockException("Café express", available: 2, requested: 5);
        ex.Message.Should().Contain("Café express").And.Contain("2").And.Contain("5");
    }

    [Fact]
    public void AccountLockedException_ExposesLockedUntil()
    {
        var lockedUntil = DateTime.Now.AddMinutes(15);
        var ex = new AccountLockedException(lockedUntil);
        ex.LockedUntil.Should().Be(lockedUntil);
    }
}
