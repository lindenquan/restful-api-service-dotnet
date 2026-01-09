using Infrastructure.Cache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Tests.Infrastructure.Cache;

/// <summary>
/// Unit tests for CacheActionFilter.
/// These tests focus on the filter logic without mocking sealed services.
/// </summary>
public sealed class CacheActionFilterTests
{
    private readonly Mock<ILogger<CacheActionFilter>> _loggerMock;
    private readonly CacheSettings _cacheSettings;

    public CacheActionFilterTests()
    {
        _loggerMock = new Mock<ILogger<CacheActionFilter>>();
        _cacheSettings = new CacheSettings
        {
            Remote = new RemoteCacheSettings
            {
                Consistency = CacheConsistency.Strong
            }
        };
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoAttributes_ShouldExecuteNext()
    {
        // Arrange
        var filter = new CacheActionFilter(null, null, _cacheSettings, _loggerMock.Object);
        var context = CreateActionContext(HttpMethods.Get);
        var nextCalled = false;

        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        }

        // Act
        await filter.OnActionExecutionAsync(context, Next);

        // Assert
        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_GetWithNullCaches_ShouldExecuteNext()
    {
        // Arrange: filter with null caches but attributes present
        var filter = new CacheActionFilter(null, null, _cacheSettings, _loggerMock.Object);
        var localCacheAttr = new LocalCacheAttribute { KeyPrefix = "test-prefix" };
        var context = CreateActionContext(HttpMethods.Get, localCacheAttr);
        var nextCalled = false;

        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        }

        // Act
        await filter.OnActionExecutionAsync(context, Next);

        // Assert - should execute next even with attributes if caches are null
        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_NonGetMethod_ShouldExecuteNext()
    {
        // Arrange
        var filter = new CacheActionFilter(null, null, _cacheSettings, _loggerMock.Object);
        var context = CreateActionContext(HttpMethods.Options);
        var nextCalled = false;

        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        }

        // Act
        await filter.OnActionExecutionAsync(context, Next);

        // Assert
        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WriteWithInvalidateKeys_ShouldExecuteNext()
    {
        // Arrange: POST request with InvalidateKeys configured
        var filter = new CacheActionFilter(null, null, _cacheSettings, _loggerMock.Object);
        var remoteCacheAttr = new RemoteCacheAttribute
        {
            InvalidateKeys = ["patients:{id}", "documents:*"]
        };
        var context = CreateActionContextWithRouteParams(
            HttpMethods.Post,
            new Dictionary<string, object?> { ["id"] = "123" },
            remoteCacheAttr);
        var nextCalled = false;

        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        }

        // Act
        await filter.OnActionExecutionAsync(context, Next);

        // Assert - action should execute even with InvalidateKeys
        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_DeleteWithMultipleInvalidateKeys_ShouldExecuteNext()
    {
        // Arrange: DELETE request with multiple InvalidateKeys
        var filter = new CacheActionFilter(null, null, _cacheSettings, _loggerMock.Object);
        var remoteCacheAttr = new RemoteCacheAttribute
        {
            InvalidateKeys = ["patients:{patientId}", "documents:{patientId}:*", "prescriptions:*"]
        };
        var context = CreateActionContextWithRouteParams(
            HttpMethods.Delete,
            new Dictionary<string, object?> { ["patientId"] = "456" },
            remoteCacheAttr);
        var nextCalled = false;

        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(context));
        }

        // Act
        await filter.OnActionExecutionAsync(context, Next);

        // Assert
        nextCalled.ShouldBeTrue();
    }

    private static ActionExecutingContext CreateActionContext(
        string httpMethod,
        params object[] endpointMetadata)
    {
        return CreateActionContextWithRouteParams(httpMethod, null, endpointMetadata);
    }

    private static ActionExecutingContext CreateActionContextWithRouteParams(
        string httpMethod,
        Dictionary<string, object?>? routeParams,
        params object[] endpointMetadata)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = httpMethod;

        var actionDescriptor = new ActionDescriptor
        {
            EndpointMetadata = endpointMetadata.ToList()
        };

        var routeData = new RouteData();
        routeData.Values["controller"] = "test";
        routeData.Values["action"] = "get";

        // Add custom route parameters
        if (routeParams != null)
        {
            foreach (var (key, value) in routeParams)
            {
                routeData.Values[key] = value;
            }
        }

        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);

        // Create action arguments from route params for parameter substitution
        var actionArguments = routeParams != null
            ? new Dictionary<string, object?>(routeParams)
            : new Dictionary<string, object?>();

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            actionArguments,
            new object());
    }

    private static ActionExecutedContext CreateExecutedContext(ActionExecutingContext context)
    {
        return new ActionExecutedContext(
            context,
            new List<IFilterMetadata>(),
            new object())
        {
            Result = new OkResult()
        };
    }
}

