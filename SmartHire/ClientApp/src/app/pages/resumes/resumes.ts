import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ResumeSummary, ResumesService } from '../../core/services/resumes.service';

@Component({
  selector: 'app-resumes',
  imports: [CommonModule],
  templateUrl: './resumes.html',
  styleUrl: './resumes.scss',
})
export class Resumes implements OnInit {
  readonly resumes = signal<ResumeSummary[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  constructor(private readonly resumesService: ResumesService) {}

  ngOnInit(): void {
    this.resumesService.getAll().subscribe({
      next: (resumes) => {
        this.resumes.set(resumes);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load resumes.');
        this.loading.set(false);
      },
    });
  }
}
