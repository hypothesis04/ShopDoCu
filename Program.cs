using Microsoft.EntityFrameworkCore;
using ShopDoCu.Models;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<ChoBanDoCuContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ChoBanDoCu")));

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(); // enable Session for layout/avatar/cart info
builder.Services.AddControllersWithViews();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

