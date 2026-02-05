using Google.Cloud.Firestore;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

string keyPath = Path.Combine(builder.Environment.ContentRootPath, "firebase-key.json");
Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", keyPath);

try
{
    FirebaseApp.Create(new AppOptions()
    {
        Credential = GoogleCredential.FromFile(keyPath),
        ProjectId = "homely-29769",
    });
}
catch (Exception ex)
{
    Console.WriteLine("Firebase Admin SDK init error: " + ex.Message);
}

builder.Services.AddSingleton(provider => FirestoreDb.Create("homely-29769"));

builder.Services.AddHttpClient();


builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
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

app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{action=Login}",
    defaults: new { controller = "Admin" });

app.MapControllerRoute(
    name: "propertymanager",
    pattern: "PropertyManager/{action=Dashboard}/{id?}",
    defaults: new { controller = "PropertyManager" });

app.Run();