import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-admin-page',
  template: '',
  standalone: true,
})
export class AdminPageComponent implements OnInit {
  constructor(private readonly router: Router) {}

  ngOnInit(): void {
    this.router.navigate(['/admin/team']);
  }
}
