$(document).ready(function () {
  $("#darkModeSwitch input").change(function () {
    if ($(this).is(":checked")) {
      $(".DashPg").addClass("darkModeOn");
    } else {
      $(".DashPg").removeClass("darkModeOn");
    }
  });

  $(".loginPg .loginForm .form-control")
    .focus(function () {
      $(this).parent().removeClass("active");
      $(this).parent().addClass("active");
    })
    .blur(function () {
      $(this).parent().removeClass("active");
      $(this).parent().addClass("active");
    });

  $(".sideNav .dropdown>a span").append('<i class="bi bi-chevron-down"></i>');

  $(".sideNav").append(
    '<a href="javascript: void(0);" class="toggleMenuLink two"></a>'
  );

  $("body").append('<div class="navOverlay"></div>');

  $(".toggleMenuLink").click(function () {
    $(".navOverlay").toggleClass("active");

    $(".toggleMenuLink").toggleClass("active");
    $(".sideNav").toggleClass("active");
    $("body").toggleClass("navOpen");

    $(".sideNav .dropdown>a").removeClass("active");
    $(".sideNav .subMenu").hide("slow");
  });
  $(".navOverlay").click(function () {
    $(this).removeClass("active");

    $(".toggleMenuLink").toggleClass("active");
    $(".sideNav").toggleClass("active");
    $("body").toggleClass("navOpen");

    $(".sideNav .dropdown>a").removeClass("active");
    $(".sideNav .subMenu").hide("slow");
  });

  $(".sideNav .dropdown>a").click(function () {
    $(this).toggleClass("active");
    $(".sideNav .dropdown>a").not(this).removeClass("active");

    $(".sideNav .dropdown>a")
      .not(this)
      .siblings(".sideNav .subMenu")
      .hide("slow");
    $(this).siblings(".sideNav .subMenu").toggle("slow");
  });

  // document.addEventListener("DOMContentLoaded", function () {
  //   const menus = document.querySelectorAll(".dropdown > a");
  //   console.log(menus); // Check if this logs the correct elements
  //   menus.forEach((menu) => {
  //     menu.addEventListener("click", function () {
  //       const subMenu = this.nextElementSibling;
  //       if (subMenu.style.display === "block") {
  //         subMenu.style.display = "none";
  //       } else {
  //         subMenu.style.display = "block";
  //       }
  //     });
  //   });
  // });
});
