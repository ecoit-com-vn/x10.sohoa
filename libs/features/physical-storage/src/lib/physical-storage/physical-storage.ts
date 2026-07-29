import { Component, OnInit } from '@angular/core';
import { PhysicalStorageService, PhysicalShelfDto, PhysicalFloorDto, PhysicalBoxDto, PagedResult } from './physical-storage.service';
import { TabViewModule } from 'primeng/tabview';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { PaginatorModule } from 'primeng/paginator';
import { CommonModule } from '@angular/common';

/**
 * Physical Storage Component
 * - Displays Shelf, Floor, Box in separate tabs.
 * - Supports pagination, lock/unlock, soft‑delete.
 * - Uses shared Core ApiService through PhysicalStorageService.
 */
@Component({
  selector: 'lib-physical-storage',
  standalone: true,
  imports: [CommonModule, TabViewModule, TableModule, ButtonModule, PaginatorModule],
  templateUrl: './physical-storage.html',
  styleUrls: ['./physical-storage.css']
})
export class PhysicalStorageComponent implements OnInit {
  // Shelf data
  shelves: PhysicalShelfDto[] = [];
  shelfTotal = 0;
  shelfPage = 1;
  shelfSize = 20;

  // Floor data
  floors: PhysicalFloorDto[] = [];
  floorTotal = 0;
  floorPage = 1;
  floorSize = 20;

  // Box data
  boxes: PhysicalBoxDto[] = [];
  boxTotal = 0;
  boxPage = 1;
  boxSize = 20;

  constructor(private storageService: PhysicalStorageService) {}

  ngOnInit(): void {
    this.loadShelves();
    this.loadFloors();
    this.loadBoxes();
  }

  // -------------------------------------------------------------------
  // Loaders
  // -------------------------------------------------------------------
  loadShelves(): void {
    const filter = { pageNumber: this.shelfPage, pageSize: this.shelfSize };
    this.storageService.getShelves(filter).subscribe(res => {
      this.shelves = res.items;
      this.shelfTotal = res.totalCount;
    });
  }

  loadFloors(): void {
    const filter = { pageNumber: this.floorPage, pageSize: this.floorSize };
    this.storageService.getFloors(filter).subscribe(res => {
      this.floors = res.items;
      this.floorTotal = res.totalCount;
    });
  }

  loadBoxes(): void {
    const filter = { pageNumber: this.boxPage, pageSize: this.boxSize };
    this.storageService.getBoxes(filter).subscribe(res => {
      this.boxes = res.items;
      this.boxTotal = res.totalCount;
    });
  }

  // -------------------------------------------------------------------
  // Lock / Unlock helpers
  // -------------------------------------------------------------------
  toggleShelfLock(shelf: PhysicalShelfDto): void {
    const action = shelf.status === 1 ? this.storageService.lockShelf(shelf.id) : this.storageService.unlockShelf(shelf.id);
    action.subscribe(() => this.loadShelves());
  }

  toggleFloorLock(floor: PhysicalFloorDto): void {
    const action = floor.status === 1 ? this.storageService.lockFloor(floor.id) : this.storageService.unlockFloor(floor.id);
    action.subscribe(() => this.loadFloors());
  }

  toggleBoxLock(box: PhysicalBoxDto): void {
    const action = box.status === 1 ? this.storageService.lockBox(box.id) : this.storageService.unlockBox(box.id);
    action.subscribe(() => this.loadBoxes());
  }

  // -------------------------------------------------------------------
  // Pagination handlers
  // -------------------------------------------------------------------
  onShelfPageChange(event: any): void {
    this.shelfPage = event.page + 1; // PrimeNG pages are zero‑based
    this.shelfSize = event.rows;
    this.loadShelves();
  }

  onFloorPageChange(event: any): void {
    this.floorPage = event.page + 1;
    this.floorSize = event.rows;
    this.loadFloors();
  }

  onBoxPageChange(event: any): void {
    this.boxPage = event.page + 1;
    this.boxSize = event.rows;
    this.loadBoxes();
  }
}

